# A machine-to-machine client the Action uses to assign roles. It can do nothing else.
resource "auth0_client" "role_assigner" {
  name        = "role-assigner"
  description = "Used by the post-login Action to assign the default role."
  app_type    = "non_interactive"
}

resource "auth0_client_credentials" "role_assigner" {
  client_id             = auth0_client.role_assigner.id
  authentication_method = "client_secret_post"
}

resource "auth0_client_grant" "role_assigner" {
  client_id = auth0_client.role_assigner.id
  audience  = local.management_api
  scopes    = ["read:roles", "create:role_members"]
}

# Assigns the default role on a user's first login.
resource "auth0_action" "assign_default_role" {
  name    = "Assign default role"
  runtime = "node22"
  deploy  = true
  code    = file("${path.module}/actions/assign-default-role.js")

  supported_triggers {
    id      = "post-login"
    version = "v3"
  }

  dependencies {
    name    = "auth0"
    version = "4.12.0"
  }

  secrets {
    name  = "AUTH0_DOMAIN"
    value = data.auth0_tenant.current.domain
  }

  secrets {
    name  = "CLIENT_ID"
    value = auth0_client.role_assigner.id
  }

  secrets {
    name  = "CLIENT_SECRET"
    value = auth0_client_credentials.role_assigner.client_secret
  }

  secrets {
    name  = "DEFAULT_ROLE_ID"
    value = auth0_role.role[local.default_role].id
  }

  depends_on = [auth0_client_grant.role_assigner]
}

resource "auth0_trigger_actions" "post_login" {
  trigger = "post-login"

  actions {
    id           = auth0_action.assign_default_role.id
    display_name = auth0_action.assign_default_role.name
  }
}
