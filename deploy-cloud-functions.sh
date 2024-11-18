#!/bin/bash

set -euo pipefail

# Default Variables
PROJECT_ID="krantenjongen"
REGION="europe-west1"
RUNTIME="dotnet8"
SERVICE_ACCOUNT="krantenjongen-functions-sa@${PROJECT_ID}.iam.gserviceaccount.com"
DRY_RUN=false

# Cloud Functions to Deploy
FUNCTIONS=(
    "PostSummaryFunction:KrantenJongen.Functions.PostSummaryFunction"
    "FilterSummaryFunction:KrantenJongen.Functions.FilterSummaryFunction"
    "BuildSummaryFunction:KrantenJongen.Functions.BuildSummaryFunction"
    "FetchArticlesFunction:KrantenJongen.Functions.FetchArticlesFunction"
)

# Log Function
log() {
    echo "$(date '+%Y-%m-%d %H:%M:%S') - $1"
}

# Error Checker
check_error() {
    if [ $? -ne 0 ]; then
        log "Error: $1"
        exit 1
    fi
}

# Authenticate and Set Project
setup_project() {
    log "Authenticating and setting project: $PROJECT_ID"
    if [ "$DRY_RUN" = false ]; then
        gcloud auth login --quiet
        gcloud config set project "$PROJECT_ID"
        check_error "Failed to authenticate or set project"
    else
        log "Dry run: Would authenticate and set project: $PROJECT_ID"
    fi
}

# Deploy Cloud Function
deploy_function() {
    local function_name=$1
    local entry_point=$2

    log "Deploying function: $function_name with entry point: $entry_point"
    if [ "$DRY_RUN" = false ]; then
        gcloud functions deploy "$function_name" \
            --entry-point "$entry_point" \
            --runtime "$RUNTIME" \
            --region "$REGION" \
            --trigger-http \
            --allow-unauthenticated \
            --service-account "$SERVICE_ACCOUNT"
        check_error "Failed to deploy function: $function_name"
    else
        log "Dry run: Would deploy function: $function_name with entry point: $entry_point"
    fi
}

# Deploy All Functions
deploy_functions() {
    log "Starting deployment of functions..."
    for fn in "${FUNCTIONS[@]}"; do
        IFS=":" read -r function_name entry_point <<< "$fn"
        deploy_function "$function_name" "$entry_point"
    done
    log "All functions deployed successfully."
}

# Main Function
main() {
    setup_project
    deploy_functions
}

# Parse Command-Line Arguments
if [ "$#" -gt 0 ] && [ "$1" == "--dry-run" ]; then
    DRY_RUN=true
    log "Dry run mode enabled."
fi

main
