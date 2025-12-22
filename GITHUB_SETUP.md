# GitHub Repository Setup Guide

This guide will help you set up your GitHub repository for Rust Desktop.

## Step 1: Initialize Git Repository

The repository has been initialized. If you need to reinitialize:

```bash
git init
```

## Step 2: Add All Files

```bash
git add .
```

## Step 3: Create Initial Commit

```bash
git commit -m "Initial commit: Rust Desktop - Unofficial Rust+ Desktop Client"
```

## Step 4: Create GitHub Repository

1. Go to [GitHub](https://github.com) and sign in
2. Click the "+" icon in the top right corner
3. Select "New repository"
4. Name it `RustDesktop` (or your preferred name)
5. **Do NOT** initialize with README, .gitignore, or license (we already have these)
6. Click "Create repository"

## Step 5: Connect Local Repository to GitHub

After creating the repository on GitHub, you'll see instructions. Run these commands:

```bash
git remote add origin https://github.com/YOUR_USERNAME/RustDesktop.git
git branch -M main
git push -u origin main
```

Replace `YOUR_USERNAME` with your actual GitHub username.

## Step 6: Verify

Visit your repository on GitHub to verify all files were uploaded correctly.

## Optional: Add GitHub Actions for CI/CD

You can add a GitHub Actions workflow for automated builds. Create `.github/workflows/build.yml`:

```yaml
name: Build

on:
  push:
    branches: [ main ]
  pull_request:
    branches: [ main ]

jobs:
  build:
    runs-on: windows-latest
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: 8.0.x
    
    - name: Restore dependencies
      run: dotnet restore
    
    - name: Build
      run: dotnet build --no-restore
    
    - name: Test
      run: dotnet test --no-build --verbosity normal
```

## Tips

- **Large Files**: If you have large files (like the `rustplus-desktop-3.0.1` folder), they're already in `.gitignore`
- **Icons**: The icons folder is included in the repository. If it's too large, consider using Git LFS
- **Secrets**: Never commit API keys, tokens, or sensitive data. They're already in `.gitignore`
- **Branches**: Consider using feature branches for new development: `git checkout -b feature-name`

## Next Steps

1. Add a description to your GitHub repository
2. Add topics/tags (e.g., `rust`, `csharp`, `wpf`, `desktop-app`, `rust-plus`)
3. Consider adding screenshots to the README
4. Set up branch protection rules if working with others
5. Enable GitHub Issues and Discussions if you want community feedback
