# Azure environment

The environment is described in three files:

| File | What |
|---|---|
| [`main.bicep`](main.bicep) | subscription scope: creates the resource group and deploys `resources.bicep` into it |
| [`resources.bicep`](resources.bicep) | the storage account (container `job-data`, table `userpaths`), the App Service plan and web app, and the role assignments |
| [`main.bicepparam`](main.bicepparam) | **the values**: resource group, region, web app and storage account names, App Service SKU. Edit them here |

Two GitHub Actions workflows use them:

| Workflow | When | What |
|---|---|---|
| [Provision Azure](../.github/workflows/provision.yml) | by hand | shows what would change (`what-if`), then runs `az deployment sub create` with `main.bicepparam` |
| [Deploy web app](../.github/workflows/deploy.yml) | every push to `main` that touches `web/`, or by hand | runs the .NET tests, builds the SPA and the API, and deploys them to the web app named in `main.bicepparam` |

**Why subscription scope:** a Bicep file deployed into a resource group can't create that group. Deploying `main.bicep` at the subscription instead lets the group's name be a parameter like the others.

**Sign-in:** both workflows sign in to Azure with **OIDC**. GitHub hands the job a short-lived token, and Azure trusts it because of a *federated credential* (branch `main`) on an app registration. So no Azure password or publish profile is stored in GitHub.

**Inside Azure:** the web app reads the storage account with its own managed identity (`STORAGE_KIND=Azure`, `STORAGE_ACCOUNT_URL`). The storage account has shared-key access switched off, so there are no account keys to leak.

## One-time setup

Run these commands once, in Git Bash, after `az login`. If `az` says "Decryption failed", run `az account clear` and then `az login` again.

### 1. App registration for GitHub

GitHub signs in as an **app registration** (Microsoft Entra ID → App registrations). Its **federated credential** has the scenario *GitHub Actions deploying Azure resources*, with organization `ptjuanramos`, repository `safari-ai-explorer`, entity type **Branch** and branch `main`. That gives the subject `repo:ptjuanramos/safari-ai-explorer:ref:refs/heads/main`, so only workflow runs on `main` can sign in.

The workflows must not set `environment:`, because that changes the subject to `...:environment:<name>` and the sign-in fails with `AADSTS700213`. If you add a GitHub environment later, add a second federated credential with entity type *Environment*.

If you made the app registration in the portal, set `APP_ID` to its Application (client) ID and skip the two `az ad app` lines. The roles below are still needed.

```bash
REPO=ptjuanramos/safari-ai-explorer
SUB_ID=$(az account show --query id -o tsv)

APP_ID=$(az ad app create --display-name github-safari-ai-explorer --query appId -o tsv)
az ad app federated-credential create --id $APP_ID --parameters "{
  \"name\": \"github-main\",
  \"issuer\": \"https://token.actions.githubusercontent.com\",
  \"subject\": \"repo:$REPO:ref:refs/heads/main\",
  \"audiences\": [\"api://AzureADTokenExchange\"]
}"

# The app's service principal (enterprise app); the portal creates it with the registration.
PRINCIPAL_ID=$(az ad sp show --id $APP_ID --query id -o tsv 2>/dev/null || az ad sp create --id $APP_ID --query id -o tsv)

# Contributor on the subscription: the deployment creates the resource group, then everything in it.
az role assignment create --assignee-object-id $PRINCIPAL_ID --assignee-principal-type ServicePrincipal \
  --role Contributor --scope /subscriptions/$SUB_ID

# Role Based Access Control Administrator lets the Bicep file give out the storage roles. The condition restricts it
# to the three storage data roles below (Blob Data Reader, Blob Data Contributor, Table Data Contributor), so the
# GitHub identity can't make anyone Owner.
ROLES='2a2b9908-6ea1-4ae2-8e65-a410df84e7d1, ba92f5b4-2d11-453d-a403-e96b0029c9fe, 0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'
CONDITION='((!(ActionMatches{'"'"'Microsoft.Authorization/roleAssignments/write'"'"'})) OR (@Request[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals {'"$ROLES"'})) AND ((!(ActionMatches{'"'"'Microsoft.Authorization/roleAssignments/delete'"'"'})) OR (@Resource[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals {'"$ROLES"'}))'
az role assignment create --assignee-object-id $PRINCIPAL_ID --assignee-principal-type ServicePrincipal \
  --role "Role Based Access Control Administrator" --scope /subscriptions/$SUB_ID \
  --condition "$CONDITION" --condition-version 2.0

# The values for step 3:
echo $APP_ID                                   # AZURE_CLIENT_ID
az account show --query tenantId -o tsv        # AZURE_TENANT_ID
echo $SUB_ID                                   # AZURE_SUBSCRIPTION_ID
az ad signed-in-user show --query id -o tsv    # AZURE_DATA_UPLOADER_ID (you)
```

### 2. Names in `main.bicepparam`

The web app and storage account names are **global across Azure**. If the provision run says one is taken, change it in [`main.bicepparam`](main.bicepparam) and run again.

| Parameter | Default | Note |
|---|---|---|
| `resourceGroupName` | `rg-safari-explorer` | created by the deployment if missing |
| `location` | `westeurope` | any region with App Service Linux |
| `webAppName` | `safari-ai-explorer` | the site is `https://<name>.azurewebsites.net` |
| `storageAccountName` | `safariaiexplorer` | 3–24 lowercase letters and digits |
| `appServiceSku` | `B1` | about 13 USD a month; `F1` is free but sleeps and has a daily CPU quota |

### 3. GitHub variables and secrets

Add these as **repository** variables and secrets, not environment ones, under the repo's **Settings → Secrets and variables → Actions**.

**Variables.** These ids aren't secret: they're useless without the federated trust.

| Name | Value |
|---|---|
| `AZURE_CLIENT_ID` | the app registration's Application (client) ID |
| `AZURE_TENANT_ID` | your tenant id |
| `AZURE_SUBSCRIPTION_ID` | your subscription id |
| `AZURE_DATA_UPLOADER_ID` | optional: your object id, so you can upload runs and import paths |

**Secrets.** `main.bicepparam` reads them from the environment with `readEnvironmentVariable`, so they're never in git.

| Name | Value |
|---|---|
| `GOOGLE_MAPS_API_KEY` | the server key (Routes and Roads APIs), stored as an app setting of the web app |
| `VITE_GOOGLE_MAPS_BROWSER_KEY` | the browser key. It's built into the SPA, so restrict it in Google Cloud to `https://<webapp>.azurewebsites.net/*` |

### 4. Provision, upload the data, deploy

1. **Provision:** Actions → **Provision Azure** → Run workflow. Tick *what-if only* first if you want to see the plan. The run summary shows the site URL.
2. **Upload the job files:** the commands are in CLAUDE.md under *Storage account*. Use `--auth-mode login`. Account keys are off, so `--account-key` and connection strings won't work. A new role assignment can take a few minutes to apply.
3. **Import the saved paths:** run `STORAGE_ACCOUNT_URL=https://<account>.blob.core.windows.net dotnet run --project web/src/SafariExplorer.Api -- import-user-paths data`.
4. **Deploy:** Actions → **Deploy web app** → Run workflow. After that it runs on every push to `main` that touches `web/`.

## Changing the environment

- **Editing the environment:** edit `main.bicepparam` (values) or `resources.bicep` (resources) and run **Provision Azure** again. Running it again changes only what differs. A setting changed by hand in the portal is reset if the Bicep file sets it too.
- **Renaming the web app** creates a new one; the old one stays until you delete it. Deploy web app follows the new name in `main.bicepparam`.
- **App Service runtime:** the web app runs on the `DOTNETCORE|11.0` stack (`linuxFxVersion`). If provisioning says the stack doesn't exist yet, check with `az webapp list-runtimes --os linux`.
- **Removing everything:** run `az group delete -n rg-safari-explorer`. The app registration and its subscription roles stay, so Provision Azure can create the environment again.
