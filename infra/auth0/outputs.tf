output "tenant_domain" {
  description = "Auth0 tenant domain; the API's Auth0__Domain setting."
  value       = data.auth0_tenant.current.domain
}

output "api_identifier" {
  description = "Access-token audience; the API's Auth0__Audience setting."
  value       = auth0_resource_server.api.identifier
}

output "spa_client_id" {
  description = "Client ID the Angular frontend logs in with."
  value       = auth0_client.spa.id
}
