pipeline {
    agent any

    environment {
        DOTNET_CLI_HOME = "C:\\Windows\\Temp"
        PUBLISH_DIR     = "C:\\publish_output"
        IIS_SITE_PATH   = "E:\\test"
        APP_NAME        = "modaar.api"
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
                bat "dotnet publish --configuration Release --output %PUBLISH_DIR% --no-build"
            }
        }

        stage('Deploy to IIS') {
            steps {
                bat 'iisreset /stop'
                bat "xcopy /E /I /Y \"%PUBLISH_DIR%\\*\" \"%IIS_SITE_PATH%\\\""
                bat 'iisreset /start'
            }
        }
    }

    post {
        success {
            echo 'Deployment successful! Site is live on IIS.'
        }
        failure {
            echo 'Pipeline failed. Check the logs above.'
        }
    }
}