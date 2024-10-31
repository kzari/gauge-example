# Posting Catalogs

## Post json catalog and check if it was created
Post a new Json catalog to Postman and verify if the database was created.

* Given S3 buket is "uux-itaas-integration-tests", key is "postman-tests/download/little_catalog.zip", instance is "01", name is "json-catalog", language is "pt-br".
* Post Json Catalog on Postman.
* Assert Post result is 200.
* Assert the catalog db instanceId "01" exists on Prometheus.

## Post json catalog and search on Prometheus

* Given S3 buket is "uux-itaas-integration-tests", key is "postman-tests/download/little_catalog.zip", instance is "01", name is "json-catalog", language is "pt-br".
* Post Json Catalog on Postman.
* Assert Post result is 200.
* Call prometheus on "contents/all".