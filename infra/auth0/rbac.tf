# Roles are bundles of permissions. Auth0 roles don't inherit, so each role lists every permission it grants.
resource "auth0_role" "role" {
  for_each = local.roles

  name        = each.value.name
  description = each.value.description
}

resource "auth0_role_permissions" "role" {
  for_each = local.roles

  role_id = auth0_role.role[each.key].id

  dynamic "permissions" {
    for_each = each.value.permissions
    content {
      name                       = permissions.value
      resource_server_identifier = auth0_resource_server.api.identifier
    }
  }

  depends_on = [auth0_resource_server_scopes.api]
}
