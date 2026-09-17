locals {
  # infra/auth0/permissions.json is the single source of truth for permissions and roles (docs/spec.md §5.1).
  manifest = jsondecode(file("${path.module}/permissions.json"))

  roles = { for role in local.manifest.roles : role.name => role }

  # The role the post-login Action assigns on a user's first login. Exactly one role may be the default.
  default_roles = [for role in local.manifest.roles : role.name if try(role.default, false)]
  default_role  = one(local.default_roles)

  management_api = "https://${data.auth0_tenant.current.domain}/api/v2/"
}
