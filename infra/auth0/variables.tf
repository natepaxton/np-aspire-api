variable "api_identifier" {
  description = "Identifier of the Auth0 API. This is the access-token audience and the API's Auth0__Audience setting."
  type        = string
}

variable "api_name" {
  description = "Display name of the Auth0 API."
  type        = string
  default     = "np-aspire API"
}

variable "api_token_lifetime" {
  description = "Access-token lifetime in seconds. Kept short so permission changes take effect quickly."
  type        = number
  default     = 3600
}

variable "spa_name" {
  description = "Display name of the SPA application (the Angular frontend)."
  type        = string
  default     = "np-aspire"
}

variable "spa_urls" {
  description = "Origins the SPA is served from, e.g. http://localhost:4300. Used for callback, logout, and CORS URLs."
  type        = list(string)
}

variable "create_test_users" {
  description = "Create the test users (dev tenant only)."
  type        = bool
  default     = false
}

variable "test_user_email_domain" {
  description = "Email domain for the test users."
  type        = string
  default     = "np-aspire.test"
}

variable "test_user_password" {
  description = "Password for the test users. Comes from TF_VAR_test_user_password; never committed."
  type        = string
  sensitive   = true
  default     = ""
}
