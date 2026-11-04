---
sidebar_label: Data Connectors
title: Data Connectors
description: Connect to databases, search indices, APIs and files, and make their data available to reports and data pipelines.
technical_manual:
  - modules/data-connectors
---

Data connectors bring data from other systems into the site, so you can use it in reports and data pipelines next to the site's own data. You set up a **connection** to a system once, such as a SQL Server database, an Elasticsearch cluster, a web API or a folder of files, and then add **data sets** that read from it: a table, an index, an API address or a file.

Each data set is read in one of two ways:

- **Live**: every report or pipeline that reads it asks the other system, so it always sees the current rows.
- **Imported**: the rows are copied into the site on a schedule you choose, and reports read the copy. Reports are faster, and keep working when the other system is down.

| | |
| --- | --- |
| **Menu** | Tools > Data Connectors > Connections, and Tools > Data Connectors > Data Sets |
| **Permissions** | Manage data connections and their credentials (connections); Manage connected data sets (data sets); Refresh imported data sets (Refresh now); Read every connected data set, or Read the connected data set - *name*, to read a data set in a report or pipeline |
| **Features** | Data Connectors, with at least one of HTTP API Data Connector, Elasticsearch Data Connector, Media Files Data Connector and SQL Server Data Connector |

<AskYourAdmin />

## Add a connection

<YouTube video="data-connectors-connections" title="Video overview of connections: the four kinds, Save and test, secrets, and deleting" />

1. Go to **Tools > Data Connectors > Connections** and click **Add Connection**.
2. In **Which kind of system should the connection reach?**, find the kind of system (type in **Filter** to narrow the list), then click **Add** on its card.
3. Enter a **Name** and the settings of the system (see the tables below).
4. Click **Save and test**. The site saves the connection and checks that it can reach the system. A green message says what it found, such as the server's version; a red message says why it failed.

Passwords, keys and secrets are stored encrypted and are never shown again. When you edit a connection, leave a secret box empty to keep the stored secret, or type a new one to replace it.

To check a saved connection again, open **Actions** on its row and click **Test**. You can't delete a connection while data sets read from it.

### SQL Server

| Field | What it does |
| --- | --- |
| **Server** | The server's name, such as *sql.contoso.com*, *sql.contoso.com,1433* or *server\\instance*. |
| **Database** | The database to read. |
| **Sign in with** | **A SQL Server login**, **The site's Windows account**, **A Microsoft Entra service principal**, **The managed identity of the site's host**, or **The default Microsoft Entra credential of the site's host**. |
| **Login or client ID**, **Password or client secret** | The credentials, when the sign-in needs them. |
| **Encrypt the connection** | Encrypts the traffic. Azure SQL needs it. |
| **Trust the server's certificate** | Accepts a certificate the site can't check, such as a self-signed one. Use it only for a server you control. |
| **Read from a read-only replica when there is one** | Sends reports to a read-only copy of the database, so they don't slow the main one down. |
| **Connect timeout (seconds)** | How long to wait for the server. |

Ask your database administrator for a login that can only read what you need: a data set reads with the rights of its connection.

### HTTP API

| Field | What it does |
| --- | --- |
| **Base URL** | The address every request starts with, such as *https://api.contoso.com/v1*. |
| **Authentication** | **None**, **API key**, **Basic (user name and password)**, **Bearer token**, **OAuth 2.0 (token endpoint)** or **OpenID Connect (discovery)**. The fields below change with your choice. |
| **Send the key in**, **Header or parameter name**, **Prefix**, **API key** | For an API key: where it goes, its name, any text before it, and the key. |
| **User name**, **Password** | For Basic, and for the password grant of OAuth 2.0 and OpenID Connect. |
| **Bearer token** | A fixed token sent with every request. |
| **Token endpoint** | For OAuth 2.0: the address that issues tokens. |
| **Issuer (authority)** | For OpenID Connect: the address of the identity provider; the site finds its token endpoint by itself. |
| **Grant**, **Client ID**, **Client secret**, **Client authentication**, **Scopes**, **Audience** | The client the API's administrator registered for you, and what it asks for. |
| **Headers** | Extra headers, one *Name: value* per line. |
| **Timeout (seconds)** | How long one request may take. Busy servers are asked again up to three times. |

### Elasticsearch

| Field | What it does |
| --- | --- |
| **Cluster URL** | The address of the cluster, such as *https://search.contoso.com:9200*. |
| **Authentication** | **None**, **API key** (sent as *ApiKey* in the Authorization header), **Basic (user name and password)** or **Bearer token**. |

It also reads OpenSearch clusters.

### Media library files

| Field | What it does |
| --- | --- |
| **Folder** | The folder of the media library whose files data sets can read, with its subfolders. Leave it empty for the whole library. |

Upload CSV, JSON, JSON Lines or Excel files to that folder with **Media > Library**. Out of the box the media library accepts Excel files only: your administrator has to allow CSV, JSON and JSON Lines in the site's media settings before you can upload them, and the library does not list them until then.

The site can't reach servers on its own private network unless your administrator allows them. If a check fails with a message about the site's own network, ask your administrator.

## Add a data set

<YouTube video="data-connectors-data-sets" title="Video overview of adding data sets from a file and a web API, and reviewing their fields" />

1. Go to **Tools > Data Connectors > Data Sets** and click **Add Data Set**, or open **Actions** on a connection and click **Add data set**.
2. In **Which connection should the new data set read?**, find the connection and click **Add** on its card. The connections are grouped by the kind of system they reach: click a group on the left, such as **SQL Server** or **HTTP API**, to see only its connections, or type in **Filter** to search their names and descriptions.
3. Enter a **Name**. The **Technical name** is what reports and pipelines store; it is made from the name if you leave it empty, and it can't change after you save.
4. Pick the **Resource**: start typing to choose from what the connection lists, such as a table, a view, an index or a file. For an HTTP API, type the path, such as */orders?status=open*.
5. Choose **Live** or **Imported** under **Access**. A connector that can do only one shows the other as unavailable.
6. Click **Discover fields**. The table lists the fields the source has. For each one you can:
   - clear **Show** to hide it from reports;
   - change its **Name** and **Display name**;
   - change its **Type**, for example to read a text column as a number;
   - tick **Identifier** for the fields that identify a row, such as an ID.
7. Pick a **Default date field** if reports should filter by a date by default.
8. Fill in the settings of the connector (see below), then click **Save**.

If you save without discovering the fields, the site reads them for you. An imported data set is queued for its first import as soon as you save it, and the site's background task runs it within a minute: until then its row in the list says the first import is waiting to run.

### Settings by connector

| Connector | Field | What it does |
| --- | --- | --- |
| SQL Server | **Custom query** | Instead of a table or view, one SELECT query, such as one that joins two tables. Leave it empty to read the resource. |
| SQL Server | **Query timeout (seconds)** | How long a query may run. |
| HTTP API | **Method**, **JSON body** | GET, or POST with a body. |
| HTTP API | **Response format** | JSON, CSV or JSON Lines. |
| HTTP API | **Records path** | Where the rows are in a JSON answer, such as *data.items*. Leave it empty when the answer is the list itself. |
| HTTP API | **Pages** and the page fields | How the API splits long answers: by page number, by offset, with a cursor, with a link to the next page in the answer, or in the Link header. **Most pages** stops a read that never ends. |
| Elasticsearch | **Query** | Picks the documents to read; leave it empty for all of them. The resource can be an index, an alias, or a pattern such as *logs-\**. |
| Elasticsearch | **Scroll keep-alive (minutes)** | How long the cluster keeps a read open between pages. |
| Media library files | **CSV delimiter**, **The first row names the columns**, **Excel sheet** | How to read the file. |

## Keep an imported data set up to date

<YouTube video="data-connectors-refresh" title="Video of refreshing imported data sets: schedule, Refresh now, history and failed refreshes" />

In the **Refresh** section of an imported data set:

| Field | What it does |
| --- | --- |
| **Refresh every (minutes)** | How often the site imports the rows again. Leave it empty to refresh only when you ask. |
| **Refresh reads** | **Every row**, or **Only the rows changed since the last refresh** for SQL Server and Elasticsearch. Changed rows replace the rows with the same identifier, so tick **Identifier** on the fields that identify a row. A row deleted at the source stays until the next refresh that reads every row. |
| **Changed rows have a larger** | For changed rows only: a modification date or a version number that grows when a row changes. |
| **When the source's fields change** | Add the new fields and keep the removed ones empty, or fail the refresh and keep the rows you have until you review the fields. |

To refresh now, open **Actions** on the data set's row and click **Refresh now**. The refresh is queued, and the background task runs it within a minute: the row shows **Refresh waiting** until it does. The list shows the result of the last refresh, how many rows it published and, when it failed, why. A refresh that fails never removes rows: reports keep reading the rows of the last refresh that worked.

To see every refresh, open **Actions** and click **History**. Each entry shows when it ran, who or what started it, whether it read every row or only changed ones, how many rows it read and published, and any change of the source's fields or error.

## Look at the rows

Open **Actions** on a data set and click **Preview**. You see its first rows exactly as reports and pipelines read them: live from the source, or from the last import.

## Use a data set in a report or pipeline

<YouTube video="data-connectors-reports-and-pipelines" title="Video of using live and imported data sets in the Report Builder and in a data pipeline" />

Live data sets are listed under **Connected data (live)** and imported ones under **Connected data (imported)**, grouped by their connection, wherever you pick a data source: in the [Report Builder](report-builder/data-sources.md) and in a data pipeline's **Read a data source** step.

You only see a data set there when your role has **Read every connected data set**, or **Read the connected data set** with that data set's name. This is the same for live and imported data sets, so importing data never shows it to more people. Ask your administrator to grant the permission in **Access Control > Roles**.

## Copy connections and data sets to another site

<YouTube video="data-connectors-deployment-and-permissions" title="Video of the permissions of Data Connectors, deployment plans and troubleshooting" />

You can move your connections and data sets to another site, such as from a staging site to the live one, with a deployment plan.

1. Go to **Configuration > Import/Export > Deployment Plans** and create a plan, or open one.
2. Click **Add Step**, open the **Data** group, and add **Data Connections**. Then add **Connected Data Sets**. Keep the connections first: each data set needs its connection on the other site.
3. In each step, leave **Include all connections** (or **Include all data sets**) ticked, or clear it and tick the ones to copy.
4. Run the plan to download its file, or send it to the other site, and import it there with **Configuration > Import/Export > Package Import**.

On the other site:

- A connection or data set with the same identifier or technical name is updated; the others are created.
- **Passwords, keys and secrets are not copied**, because they are encrypted for this site only. Open each copied connection, type its secrets, and click **Save and test**. Your administrator can also put them in the import file.
- **Imported data sets don't bring their rows.** Each one starts its first import on the other site within a minute, from its own connection.
- The other site needs the same connector features enabled, and must be able to reach the systems.

Your administrator can also create connections and data sets with a setup or import recipe; the [Technical Manual](../modules/data-connectors.md#recipes-and-deployment-plans) describes its steps.

| | |
| --- | --- |
| **Permissions** | Manage deployment plans and Export data on this site; Import data on the other site |
| **Features** | Deployment, and Recipes on the importing site |

## Troubleshooting

| Problem | What to do |
| --- | --- |
| The connection check says the host *is on the site's own network* | The site may not reach that server. Ask your administrator to allow it. |
| *The token endpoint refused the credentials* | Check the client ID, client secret and scopes with whoever runs the identity provider. |
| A data set is missing from reports | Check that it is enabled, that its fields were read, and that your role can read it. An imported data set shows no rows until its first refresh succeeds. |
| *The source's fields changed* | The source added, removed or retyped fields. Open the data set, click **Discover fields**, review the table and save. |
| A refresh keeps failing | Open **History** for the error. The last good rows stay available while you fix it. |
