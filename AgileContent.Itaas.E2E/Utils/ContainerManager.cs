using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AgileContent.Itaas.E2E.Models;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Testcontainers.PostgreSql;

namespace AgileContent.Itaas.E2E;
public class ContainerManager {
    private readonly string _scriptsFolder;
    private readonly string _userFolder;
    private readonly string _postmanImageTag;
    private readonly string _prometheusImageTag;
    private readonly string _credentialsPath;
    private readonly string _awsCredentialRelativeUri;
    private readonly bool _copyAwsCredentials;
    private readonly string _prestigeImageTag;
    private readonly INetwork _network;
    private readonly List<IContainer> _containers = [];

    public ContainerManager() {
        _network = new NetworkBuilder()
            .WithName(Guid.NewGuid().ToString("D"))
            .Build();

        _scriptsFolder = Path.Combine("/home/jsouza/proj/itaas-e2e/AgileContent.Itaas.E2E/Scripts", "");
        _postmanImageTag = GetEnvOrDefault("POSTMAN_DOCKER_TAG", "stable");
        _prestigeImageTag = GetEnvOrDefault("PRESTIGE_DOCKER_TAG", "stable");
        _prometheusImageTag = GetEnvOrDefault("PROMETHEUS_DOCKER_TAG", "stable");
        _userFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _credentialsPath = $"{_userFolder}/.aws/credentials";
        _awsCredentialRelativeUri = Environment.GetEnvironmentVariable("AWS_CONTAINER_CREDENTIALS_RELATIVE_URI");
        _copyAwsCredentials = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CODEBUILD_BUILD_ID"));
    }

    public async Task<ContainersInfo> StartAsync() {
        List<string> prestigeHosts = [];
        List<IContainer> prometheusContainers = [];

        var useOpenSearch = true;
        var usePrestige = true;

        var (osAlias, osContainer) = useOpenSearch ? GetOpensearch() : default;
        
        var (pgAliasA, pgContainerA) = GetPostgres();

        var (prestigeAliasA, prestigeContainerA) = usePrestige ? GetPrestige(pgAliasA, osAlias, pgContainerA) : default;

        var postmanContainer = GetPostman(pgAliasA, pgContainerA, osAlias, osContainer);

        var (doryAlias, doryContainer) = useOpenSearch ? GetDory(osAlias, osContainer) : default;

        var prometheusContainer = GetPrometheus(pgAliasA, pgContainerA);

        var (whAlias, whContainer) = GetWebhook();
        
        var tasks = _containers.Select(c => c.StartAsync());
        await Task.WhenAll(tasks).ConfigureAwait(false);

        var postmanUrl = GetHostFromContainerPort(postmanContainer, 9601);
        var prometheusUrl = GetHostFromContainerPort(prometheusContainer, 9700);
        var whHost = GetHostFromContainerPort(whContainer, 80);
        var osHost = GetHostFromContainerPort(osContainer, 9200);
        //string osHost = null;

        static string WithHttp (string str) => $"http://{str}";

        return new ContainersInfo(osHost, postmanUrl, prestigeAliasA, prometheusUrl, WithHttp(whAlias), whHost);
    }
    
    internal async Task DestroyAsync() {
        foreach (var item in _containers) {
            await item.StopAsync();
            await item.DisposeAsync();
        }
        await _network.DeleteAsync();
    }

    private (string alias, IContainer container) GetDory(string osAlias, IContainer osContainer) {
        var alias = GetRandomAlias("dory");
        var container = new ContainerBuilder()
        .DependsOn(osContainer)
        .WithImage($"873339144623.dkr.ecr.us-east-1.amazonaws.com/agilecontent/dory:alpha")
        .WithEnvironment("DORY_OPENSEARCH_HOST", osAlias)
        .WithEnvironment("DORY_USE_CLICK", "true")
        .WithNetwork(_network)
        .WithNetworkAliases(alias)
        .Build();

        _containers.Add(container);
        return (alias, container);
    }

    private (string alias, IContainer container) GetOpensearch() {
        var alias = GetRandomAlias("os");
        var container = new ContainerBuilder()
        .WithImage("opensearchproject/opensearch:2.11.0")
        .WithPortBinding(9200, true)
        .WithEnvironment("discovery.type", "single-node")
        .WithEnvironment("node.name", "dory-opensearch")
        .WithEnvironment("plugins.security.disabled", "true")
        .WithEnvironment("OPENSEARCH_JAVA_OPTS", "-Xms512m -Xmx512m")
        .WithNetwork(_network)
        .WithNetworkAliases(alias)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request =>
            request
            .ForPort(9200)
            .ForPath("/")
            .ForResponseMessageMatching(r => Task.FromResult(r.IsSuccessStatusCode))))
        .Build();
        _containers.Add(container);

        return (alias, container);
    }
    private (string alias, PostgreSqlContainer container) GetPostgres() {
        var alias = GetRandomAlias("pg");
        var container = new PostgreSqlBuilder()
            .WithImage("postgres:13-alpine")
            .WithPassword("iwannarock")
            .WithCommand("-c", "max_connections=10000")
            .WithCommand("-c", "checkpoint_completion_target=0.9")
            .WithCommand("-c", "checkpoint_flush_after=32")
            .WithCommand("-c", "checkpoint_timeout=300")
            .WithCommand("-c", "checkpoint_warning=30")
            .WithCommand("-c", "max_wal_size=16384")
            .WithCommand("-c", "min_wal_size=4096")
            .WithNetwork(_network)
            .WithNetworkAliases(alias)
            .WithResourceMapping(_scriptsFolder, "/docker-entrypoint-initdb.d/")
            .Build();
        _containers.Add(container);

        return (alias, container);
    }

    private (string alias, IContainer container) GetPrestige(string dbHost, string osAlias, IContainer pgContainer) {
        var alias = GetRandomAlias("prestige");
        var container = new ContainerBuilder()
        .DependsOn(pgContainer)
        .WithImage($"873339144623.dkr.ecr.us-east-1.amazonaws.com/uux/prestige:{_prestigeImageTag}")
        .WithPortBinding(9602, true)
        .WithEnvironment("PRESTIGE_DB_HOST", dbHost)
        .WithEnvironment("AWS_DEFAULT_REGION", "us-east-1")
        .WithEnvironment("PRESTIGE_OPENSEARCH_HOST", osAlias)
        .WithEnvironment("AWS_CONTAINER_CREDENTIALS_RELATIVE_URI", _awsCredentialRelativeUri)
        .WithCondition(_copyAwsCredentials, b => b.WithResourceMapping(_credentialsPath, "/root/.aws/"))
        .WithNetwork(_network)
        .WithNetworkAliases(alias)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request =>
            request
            .ForPort(9602)
            .ForPath("/v1/servicestatus")
            .ForResponseMessageMatching(r => Task.FromResult(r.IsSuccessStatusCode))))
        .Build();
        _containers.Add(container);

        return ($"{alias}:{9602}", container);
    }
    private IContainer GetPostman(string dbHost, PostgreSqlContainer pgContainer, string osHost, IContainer osContainer) {
        var container = new ContainerBuilder()
        .DependsOn(pgContainer)
        .WithCondition(osHost is not null, b => b.DependsOn(osContainer))
        .WithImage($"873339144623.dkr.ecr.us-east-1.amazonaws.com/uux/postman:{_postmanImageTag}")
        .WithPortBinding(9601, true)
        .WithEnvironment("POSTMAN_DB_HOST", dbHost)
        .WithEnvironment("AWS_DEFAULT_REGION", "us-east-1")
        .WithEnvironment("POSTMAN_EXTERNAL_URL_TIMEOUT_MILLISECONDS", "60000")
        .WithEnvironment("POSTMAN_FeatureManagement__NpgsqlDisableDateTimeInfinityConversions", "true")
        .WithEnvironment("POSTMAN_OPENSEARCH_HOST", osHost)
        .WithEnvironment("POSTMAN_OPENSEARCH_INDEX_REFRESH_INTERVAL", "1s")
        .WithEnvironment("POSTMAN_USE_CLICK", "true")
        .WithEnvironment("AWS_CONTAINER_CREDENTIALS_RELATIVE_URI", _awsCredentialRelativeUri)
        //.WithEnvironment("POSTMAN_FeatureManagement__UsePrestige", "false")
        .WithCondition(_copyAwsCredentials, b => b.WithResourceMapping(_credentialsPath, "/root/.aws/"))
        .WithNetwork(_network)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request =>
            request
            .ForPort(9601)
            .ForPath("/")
            .ForResponseMessageMatching(r => Task.FromResult(r.IsSuccessStatusCode))))
        .Build();
        _containers.Add(container);
        
        return container;
    }
    
    private IContainer GetPrometheus(string dbHost, PostgreSqlContainer pgContainerA) {
        var container = new ContainerBuilder()
        .DependsOn(pgContainerA)
        .WithImage($"873339144623.dkr.ecr.us-east-1.amazonaws.com/uux/prometheus:{_prometheusImageTag}")
        .WithPortBinding(9700, true)
        .WithEnvironment("PROMETHEUS_DB_HOST", dbHost)
        .WithEnvironment("PROMETHEUS_NOTRECOMMENDED_EnableNonPagedSchedules", "true")
        .WithEnvironment("PROMETHEUS_DB_CACHE_DURATION_SECONDS", "0")
        .WithEnvironment("PROMETHEUS_USE_CLICK", "true")
        .WithEnvironment("AWS_CONTAINER_CREDENTIALS_RELATIVE_URI", _awsCredentialRelativeUri)
        .WithCondition(_copyAwsCredentials, b =>
            b.WithResourceMapping(_credentialsPath, "/root/.aws/"))
        .WithNetwork(_network)
        .Build();
        _containers.Add(container);

        return container;
    }
    private (string alias, IContainer container) GetWebhook() {
        var alias = GetRandomAlias("wh");
        var container = new ContainerBuilder()
        .WithImage("873339144623.dkr.ecr.us-east-1.amazonaws.com/uux/itaas-webhook:no-rate-limit")
        .WithPortBinding(80, true)
        .WithNetwork(_network)
        .WithNetworkAliases(alias)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request =>
            request
            .ForPort(80)
            .ForPath("/")
            .ForResponseMessageMatching(r => Task.FromResult(r.IsSuccessStatusCode))))
        .Build();
        _containers.Add(container);

        return (alias, container);
    }

    private static string GetRandomAlias(string prefix) {
        return $"{prefix}-{Guid.NewGuid()}";
    }
    private static string GetEnvOrDefault(string env, string @default) {
        var var = Environment.GetEnvironmentVariable(env);
        return string.IsNullOrEmpty(var) ? @default : var;
    }
    
    static string GetHostFromContainerPort(IContainer c, int port) {
        return $"http://{c.Hostname}:{c.GetMappedPublicPort(port)}";
    }
    
}