# it-beacon-systray

## Prerequisites

### NuGet Feed Configuration

To build and run this project locally, you must configure authentication for the private GitHub NuGet feed (`IT-Beacon`). The credentials have been removed from the project's `nuget.config` to prevent exposing secrets in source control.

You need to add the package source credentials to your global (user-level) NuGet configuration.

#### Steps:

1.  **Generate a Personal Access Token (PAT)** on GitHub:
    *   Go to **Settings** > **Developer settings** > **Personal access tokens** > **Tokens (classic)**.
    *   Generate a new token with the `read:packages` scope.

2.  **Add the Source Locally**:
    Run the following command in your terminal (PowerShell or CMD), replacing the placeholders with your GitHub username and the PAT you just generated:

    ```powershell
    dotnet nuget add source "https://nuget.pkg.github.com/IT-Beacon/index.json" --name "IT-Beacon" --username "YOUR_GITHUB_USERNAME" --password "YOUR_GITHUB_PAT" --store-password-in-clear-text
    ```

    *Note: The `--store-password-in-clear-text` flag is often required for GitHub PATs compatibility.*

Once this is done, `dotnet restore` and Visual Studio will be able to authenticate with the private feed using your user profile credentials, keeping the project file clean and secure.

### Automated Versioning

The project uses [MinVer](https://github.com/adamralph/minver) to automate versioning based on Git tags. This ensures that the assembly version always matches the tagged release in source control.

1.  **Tagging**: When you are ready to release, create a Git tag:
    ```bash
    git tag v1.0.12
    git push origin v1.0.12
    ```
2.  **Build**: The CI workflow (or local build) will automatically detect this tag and set the application version to `1.0.12`.
3.  **No Tag**: If you build a commit that isn't tagged, MinVer will append metadata (e.g., `1.0.13-alpha.0.5`), indicating it is a development build.

**Note**: Do not manually edit `<Version>` in the `.csproj` file. It has been removed to allow MinVer to handle it.