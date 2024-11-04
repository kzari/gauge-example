# Posting a catalog on Postman and searching for content on Prometheus

* Post Json Catalog on Postman with instance "01", name "json-catalog" and language "pt-br".

------------------------------------------------------------------------------------------------------
## Check if the catalog was created

* Assert the catalog db instanceId "01" exists on Prometheus.

## Content is present on content/all

* Call prometheus on "contents/all".
* Assert the content pid list contains "AGE173,AGE_TNONE,MOV420,MOV420959".

## Content MOV420959 contains child AGE_TNONE

* Call prometheus on "content/MOV420959/children".
* Assert the content pid list contains "AGE_TNONE".