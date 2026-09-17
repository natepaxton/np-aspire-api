# The Angular frontend (np-web). Authorization Code + PKCE; no client secret is involved.
# The Auth0 application keeps the name np-aspire: renaming it is cosmetic and would churn the tenant.
resource "auth0_client" "spa" {
  name            = var.spa_name
  app_type        = "spa"
  oidc_conformant = true

  callbacks           = var.spa_urls
  allowed_logout_urls = var.spa_urls
  web_origins         = var.spa_urls

  grant_types = ["authorization_code", "refresh_token"]

  jwt_configuration {
    alg = "RS256"
  }

  # Refresh tokens keep the user logged in; rotation replaces the token on every use.
  refresh_token {
    rotation_type                = "rotating"
    expiration_type              = "expiring"
    leeway                       = 0
    token_lifetime               = 2592000 # 30 days
    idle_token_lifetime          = 1296000 # 15 days
    infinite_token_lifetime      = false
    infinite_idle_token_lifetime = false
  }
}
