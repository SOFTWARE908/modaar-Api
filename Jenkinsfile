pipeline {
    agent any

    environment {
        DOTNET_CLI_HOME = "C:\\Windows\\Temp"
        PUBLISH_DIR     = "C:\\publish_output"
        IIS_SITE_PATH   = "E:\\modaar"
        APP_NAME        = "modaar.api"

        // The admin panel (modaar.admin): its own publish folder and its own IIS site.
        ADMIN_PUBLISH_DIR   = "C:\\publish_output_admin"
        ADMIN_IIS_SITE_PATH = "E:\\modaar-admin"
    }

    stages {

        stage('Checkout') {
            steps {
                checkout scm
            }
        }

        stage('Restore') {
            steps {
                bat 'dotnet restore'
            }
        }

        stage('Build') {
            steps {
                bat 'dotnet build --configuration Release --no-restore'
            }
        }

        stage('Test') {
            steps {
                bat 'dotnet test --configuration Release --no-build'
            }
        }

        stage('Publish') {
            steps {
                // Each site to its own folder: the solution also holds modaar.core, and the API
                // and the admin panel must not land in each other's IIS folder.
                bat "dotnet publish modaar.api\\modaar.api.csproj --configuration Release --output %PUBLISH_DIR% --no-build"
                bat "dotnet publish modaar.admin\\modaar.admin.csproj --configuration Release --output %ADMIN_PUBLISH_DIR% --no-build"
            }
        }

        stage('Deploy to IIS') {
            steps {
                bat 'iisreset /stop'
                bat "xcopy /E /I /Y \"%PUBLISH_DIR%\\*\" \"%IIS_SITE_PATH%\\\""
                bat "xcopy /E /I /Y \"%ADMIN_PUBLISH_DIR%\\*\" \"%ADMIN_IIS_SITE_PATH%\\\""
                bat 'iisreset /start'
            }
        }
    }

    post {
        success {
            echo 'Deployment successful! The API and the admin panel are live on IIS.'
        }
        failure {
            echo 'Pipeline failed. Check the logs above.'
        }
    }
}