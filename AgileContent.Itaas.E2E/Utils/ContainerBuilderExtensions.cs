using System;
using DotNet.Testcontainers.Builders;

namespace AgileContent.Itaas.E2E;

static class ContainerBuilderExtensions
{
    public static ContainerBuilder WithCondition(this ContainerBuilder builder, Func<ContainerBuilder, bool> cond, Func<ContainerBuilder, ContainerBuilder> with)
    {
        return cond(builder) ? with(builder) : builder;
    }

    public static ContainerBuilder WithCondition(this ContainerBuilder builder, bool val, Func<ContainerBuilder, ContainerBuilder> with)
    {
        return val ? with(builder) : builder;
    }
}