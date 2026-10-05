#!/usr/bin/env bash
# Seeds the Key Vault secrets the API reads through Key Vault references.
# Generated secrets (JWT signing key, OTP hashing key, sandbox secret) are random and never printed.
# Provider credentials are read from environment variables so they never appear in shell history or this repo:
#   SMTP_HOST SMTP_USER SMTP_PASSWORD  SMS_API_URL SMS_API_KEY  RAZORPAY_KEY_ID RAZORPAY_KEY_SECRET RAZORPAY_WEBHOOK_SECRET
# usage: infra/set-secrets.sh <key-vault-name>
set -euo pipefail
vault="${1:?usage: set-secrets.sh <key-vault-name>}"

put() { az keyvault secret set --vault-name "$vault" --name "$1" --value "$2" --output none; echo "set $1"; }
random() { openssl rand -base64 64 | tr -d '\n'; }
exists() { az keyvault secret show --vault-name "$vault" --name "$1" --output none 2>/dev/null; }

# Rotating these signs everyone out (JWT) or invalidates pending OTPs, so they are only created when missing.
exists Jwt--SigningKey || put Jwt--SigningKey "$(random)"
exists Security--OtpHashingKey || put Security--OtpHashingKey "$(random)"
exists Payments--Sandbox--Secret || put Payments--Sandbox--Secret "$(random)"

put Email--SmtpHost "${SMTP_HOST:-localhost}"
put Email--UserName "${SMTP_USER:-unset}"
put Email--Password "${SMTP_PASSWORD:-unset}"
put Sms--ApiUrl "${SMS_API_URL:-https://sms.invalid/send}"
put Sms--ApiKey "${SMS_API_KEY:-unset}"
put Payments--Razorpay--KeyId "${RAZORPAY_KEY_ID:-unset}"
put Payments--Razorpay--KeySecret "${RAZORPAY_KEY_SECRET:-unset}"
put Payments--Razorpay--WebhookSecret "${RAZORPAY_WEBHOOK_SECRET:-unset}"
echo "Done. Restart the API web app so it re-reads Key Vault references."
