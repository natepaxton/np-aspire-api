/**
 * Post-login Action: gives a user the default role on their first login.
 *
 * Without a role a user has no permissions and gets 403 from the API, so a new user would otherwise be stuck
 * until an administrator assigned one by hand. Managed by Terraform (infra/auth0/actions.tf); do not edit in the
 * Auth0 dashboard.
 */
const { ManagementClient } = require('auth0');

exports.onExecutePostLogin = async (event, api) => {
  // Only on the first login, when the user has no roles yet.
  if (event.authorization?.roles?.length > 0) {
    return;
  }

  const management = new ManagementClient({
    domain: event.secrets.AUTH0_DOMAIN,
    clientId: event.secrets.CLIENT_ID,
    clientSecret: event.secrets.CLIENT_SECRET,
  });

  await management.users.assignRoles({ id: event.user.user_id }, { roles: [event.secrets.DEFAULT_ROLE_ID] });
};
