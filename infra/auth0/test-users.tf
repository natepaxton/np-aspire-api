# Test users for the dev tenant only (create_test_users = true). np-aspire's Playwright tests sign in as these.
locals {
  test_users = var.create_test_users ? {
    "test-member" = local.default_role
    "test-admin"  = "admin"
    # No role: proves that an authenticated user without a role gets 403.
    "test-norole" = null
  } : {}
}

resource "auth0_user" "test" {
  for_each = local.test_users

  connection_name = "Username-Password-Authentication"
  email           = "${each.key}@${var.test_user_email_domain}"
  email_verified  = true
  name            = each.key
  password        = var.test_user_password

  lifecycle {
    precondition {
      condition     = var.test_user_password != ""
      error_message = "Set TF_VAR_test_user_password in infra/auth0/.env.local before creating test users."
    }
  }
}

resource "auth0_user_roles" "test" {
  for_each = { for user, role in local.test_users : user => role if role != null }

  user_id = auth0_user.test[each.key].id
  roles   = [auth0_role.role[each.value].id]
}
