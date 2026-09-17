terraform {
  required_version = ">= 1.9"

  required_providers {
    auth0 = {
      source  = "auth0/auth0"
      version = "~> 1.0"
    }
  }
}

# Credentials come from AUTH0_DOMAIN, AUTH0_CLIENT_ID, and AUTH0_CLIENT_SECRET (infra/auth0/.env.local).
provider "auth0" {}

data "auth0_tenant" "current" {}
