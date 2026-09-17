# The API (resource server). Its identifier is the audience the SPA asks for and the API validates.
resource "auth0_resource_server" "api" {
  name        = var.api_name
  identifier  = var.api_identifier
  signing_alg = "RS256"

  token_lifetime = var.api_token_lifetime
  # Lets the SPA request refresh tokens for this API.
  allow_offline_access = true

  # RBAC: Auth0 checks the user's roles and puts their permissions in the access token.
  enforce_policies = true
  token_dialect    = "access_token_authz"

  skip_consent_for_verifiable_first_party_clients = true
}

# The permissions themselves, from permissions.json.
resource "auth0_resource_server_scopes" "api" {
  resource_server_identifier = auth0_resource_server.api.identifier

  dynamic "scopes" {
    for_each = local.manifest.permissions
    content {
      name        = scopes.value.value
      description = scopes.value.description
    }
  }
}
