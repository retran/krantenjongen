# KrantenJongen

KrantenJongen is a tool designed to gather, summarize, and share news. It fetches articles from RSS feeds (currently defined in Source.cs), translate them, generates concise summaries using Google Gemini LLMs, and publishes them to Telegram channels (currently defined in TelegramService.cs), all powered by Google Cloud.

## Steps to Deploy

### 1. Create a Google Cloud Account
- Visit [Google Cloud](https://cloud.google.com/).
- Sign up for an account (you may get free credits).
- Create a new project and note the **Project ID**.

### 2. Install Google Cloud SDK
Follow the [installation guide](https://cloud.google.com/sdk/docs/install) for your platform:
- **Linux/macOS**:
  ```bash
  curl -O https://dl.google.com/dl/cloudsdk/channels/rapid/downloads/google-cloud-sdk-<VERSION>-linux-x86_64.tar.gz
  tar -xf google-cloud-sdk-<VERSION>-linux-x86_64.tar.gz
  ./google-cloud-sdk/install.sh
  ```
- **Windows**: Download and install from the [official site](https://cloud.google.com/sdk/docs/install).

### 3. Authenticate and Set Up `gcloud`
- Authenticate your account:
  ```bash
  gcloud auth login
  ```
- Set the active project:
  ```bash
  gcloud config set project <your-project-id>
  ```

### 4. Update the Scripts
1. Open `setup-resources.sh` and `deploy-cloud-functions.sh`.
2. Replace `PROJECT_ID` with your Google Cloud Project ID:
   ```bash
   PROJECT_ID="your-project-id"
   ```
3. In `setup-resources.sh`, replace the placeholder:
   ```bash
   SECRET_VALUE="YOUR_TELEGRAM_BOT_API_KEY"
   ```

### 5. Run Resource Setup
This script configures all necessary resources:
- Enables APIs.
- Creates a service account.
- Configures secrets and queues.

```bash
chmod +x setup-resources.sh
./setup-resources.sh
```

### 6. Deploy Cloud Functions
Deploy the service’s functions using the script:
```bash
chmod +x deploy-cloud-functions.sh
./deploy-cloud-functions.sh
```

### 7. Verify Deployment
- List deployed functions:
  ```bash
  gcloud functions list --region=europe-west1
  ```

  Here’s the updated **README.md** with the GNU General Public License (GPL) included at the bottom.

## License

This project is licensed under the GNU General Public License (GPL) v3.0. You are free to modify, distribute, and use the software under the terms of the GPL.

### GPL License Terms

```text
This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program. If not, see <https://www.gnu.org/licenses/>.
```