#!/bin/bash

set -euo pipefail

# Default Variables
PROJECT_ID="krantenjongen"
REGION="europe-west1"
DATASET_ID="krantenjongen"
SERVICE_ACCOUNT="336787172409-compute@developer.gserviceaccount.com"
SECRET_NAME="TelegramBotApiKey"
SECRET_VALUE="YOUR_TELEGRAM_BOT_API_KEY"  # Replace with the actual API key
DRY_RUN=false

# Resources
QUEUES=("buildSummary" "filterSummary" "postSummary")
TABLES=("summary_embeddings" "runs")
APIS=(
    "bigquery.googleapis.com"
    "cloudtasks.googleapis.com"
    "aiplatform.googleapis.com"
    "cloudfunctions.googleapis.com"
    "logging.googleapis.com"
    "cloudscheduler.googleapis.com"
    "run.googleapis.com"
    "secretmanager.googleapis.com"
    "pubsub.googleapis.com"
    "artifactregistry.googleapis.com"
    "cloudbuild.googleapis.com"
    "iam.googleapis.com"
    "dataplex.googleapis.com"
    "gemini.googleapis.com"
)
ROLES=(
    "roles/bigquery.dataOwner"
    "roles/cloudbuild.builds.builder"
    "roles/cloudtasks.enqueuer"
    "roles/secretmanager.secretAccessor"
    "roles/aiplatform.serviceAgent"
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

# Enable APIs
enable_apis() {
    log "Enabling APIs for project: $PROJECT_ID"
    for API in "${APIS[@]}"; do
        log "Enabling $API..."
        if [ "$DRY_RUN" = false ]; then
            gcloud services enable "$API" --project="$PROJECT_ID"
            check_error "Failed to enable $API"
        else
            log "Dry run: Would enable $API"
        fi
    done
    log "APIs enabled successfully."
}

# Configure Service Account
configure_service_account() {
    log "Configuring service account: $SERVICE_ACCOUNT"
    for ROLE in "${ROLES[@]}"; do
        log "Assigning $ROLE to $SERVICE_ACCOUNT..."
        if [ "$DRY_RUN" = false ]; then
            gcloud projects add-iam-policy-binding "$PROJECT_ID" \
                --member="serviceAccount:$SERVICE_ACCOUNT" \
                --role="$ROLE" --quiet
            check_error "Failed to assign $ROLE"
        else
            log "Dry run: Would assign $ROLE to $SERVICE_ACCOUNT"
        fi
    done
    log "Service account configured successfully."
}

# Configure Secret
configure_secret() {
    log "Configuring secret: $SECRET_NAME"
    if [ "$DRY_RUN" = false ]; then
        # Check if the secret exists
        if gcloud secrets describe "$SECRET_NAME" --project="$PROJECT_ID" > /dev/null 2>&1; then
            log "Secret $SECRET_NAME already exists. Updating value..."
            echo -n "$SECRET_VALUE" | gcloud secrets versions add "$SECRET_NAME" \
                --data-file=- --project="$PROJECT_ID"
            check_error "Failed to update secret: $SECRET_NAME"
        else
            log "Secret $SECRET_NAME does not exist. Creating..."
            echo -n "$SECRET_VALUE" | gcloud secrets create "$SECRET_NAME" \
                --replication-policy="automatic" \
                --data-file=- --project="$PROJECT_ID"
            check_error "Failed to create secret: $SECRET_NAME"
        fi
    else
        log "Dry run: Would configure secret: $SECRET_NAME"
    fi
    log "Secret configured successfully."
}

# Create Scheduler Job
create_scheduler_job() {
    JOB_NAME="RunFetchArticleFunction"
    SCHEDULE="*/15 * * * *"
    TIME_ZONE="Europe/Amsterdam"
    URL="https://${REGION}-${PROJECT_ID}.cloudfunctions.net/FetchArticlesFunction"

    log "Creating scheduler job: $JOB_NAME"
    if [ "$DRY_RUN" = false ]; then
        gcloud scheduler jobs create http "$JOB_NAME" \
            --schedule="$SCHEDULE" \
            --time-zone="$TIME_ZONE" \
            --uri="$URL" \
            --http-method=GET \
            --location="$REGION" \
            --description="Run FetchArticleFunction"
        check_error "Failed to create scheduler job: $JOB_NAME"
    else
        log "Dry run: Would create scheduler job: $JOB_NAME"
    fi
    log "Scheduler job created successfully."
}

# Create Task Queues
create_queues() {
    log "Creating task queues..."
    for QUEUE in "${QUEUES[@]}"; do
        log "Creating queue: $QUEUE"
        if [ "$DRY_RUN" = false ]; then
            gcloud tasks queues create "$QUEUE" --location="$REGION" \
                --max-concurrent-dispatches=1 \
                --max-dispatches-per-second=0.1 \
                --max-burst-size=10 \
                --retry-config-max-attempts=100 \
                --retry-config-min-backoff=60s \
                --retry-config-max-backoff=3600s \
                --retry-config-max-doublings=16
            check_error "Failed to create queue: $QUEUE"
        else
            log "Dry run: Would create queue: $QUEUE"
        fi
    done
    log "Task queues created successfully."
}

# Create BigQuery Dataset and Tables
create_bigquery_resources() {
    log "Creating BigQuery dataset and tables..."
    if [ "$DRY_RUN" = false ]; then
        bq --location="$REGION" mk --dataset "$PROJECT_ID:$DATASET_ID"
        check_error "Failed to create dataset: $DATASET_ID"
    else
        log "Dry run: Would create dataset: $DATASET_ID"
    fi

    declare -A TABLE_SCHEMAS=(
        ["summary_embeddings"]="[
          {\"name\": \"id\", \"type\": \"STRING\", \"mode\": \"REQUIRED\"},
          {\"name\": \"summary\", \"type\": \"STRING\", \"mode\": \"REQUIRED\"},
          {\"name\": \"published_at\", \"type\": \"DATETIME\", \"mode\": \"REQUIRED\"},
          {\"name\": \"embedding\", \"type\": \"FLOAT64\", \"mode\": \"REPEATED\"}
        ]"
        ["runs"]="[
          {\"name\": \"run_id\", \"type\": \"STRING\", \"mode\": \"REQUIRED\"},
          {\"name\": \"timestamp\", \"type\": \"DATETIME\", \"mode\": \"REQUIRED\"}
        ]"
    )

    for TABLE in "${TABLES[@]}"; do
        log "Creating table: $DATASET_ID.$TABLE"
        if [ "$DRY_RUN" = false ]; then
            echo "${TABLE_SCHEMAS[$TABLE]}" > "${TABLE}_schema.json"
            bq mk --table \
                --description "Table: ${TABLE//_/ }" \
                --schema "${TABLE}_schema.json" \
                "$PROJECT_ID:$DATASET_ID.$TABLE"
            check_error "Failed to create table: $TABLE"
            rm "${TABLE}_schema.json"
        else
            log "Dry run: Would create table: $TABLE"
        fi
    done
    log "BigQuery resources created successfully."
}

# Delete Resources
delete_resources() {
    log "Deleting resources..."
    for QUEUE in "${QUEUES[@]}"; do
        log "Deleting queue: $QUEUE"
        if [ "$DRY_RUN" = false ]; then
            gcloud tasks queues delete "$QUEUE" --location="$REGION" --quiet || true
        fi
    done

    for TABLE in "${TABLES[@]}"; do
        log "Deleting table: $DATASET_ID.$TABLE"
        if [ "$DRY_RUN" = false ]; then
            bq rm -f -t "$PROJECT_ID:$DATASET_ID.$TABLE" || true
        fi
    done

    log "Deleting dataset: $DATASET_ID"
    if [ "$DRY_RUN" = false ]; then
        bq rm -f -d "$PROJECT_ID:$DATASET_ID" || true
    fi

    log "Deleting scheduler job..."
    if [ "$DRY_RUN" = false ]; then
        gcloud scheduler jobs delete RunFetchArticleFunction --location="$REGION" --quiet || true
    fi
    log "Resources deleted successfully."
}

# Main Function
main() {
    case "$1" in
        create)
            enable_apis
            configure_service_account
            configure_secret
            create_queues
            create_bigquery_resources
            create_scheduler_job
            ;;
        delete)
            delete_resources
            ;;
        *)
            echo "Usage: $0 {create|delete}"
            exit 1
            ;;
    esac
}

# Parse Command-Line Arguments
if [ $# -eq 0 ]; then
    echo "Usage: $0 {create|delete}"
    exit 1
fi

main "$1"
