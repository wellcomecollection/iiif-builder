# AuthTest

A very basic app that integrates with Auth0 to verify that we can pull through roles and map them to DLCS roles.

Based on [auth0-aspnetcore-mvc](https://github.com/auth0-samples/auth0-aspnetcore-mvc-samples/tree/master/Quickstart/Sample) starter app and uses [Auth0.AspNetCore.Authentication](https://www.nuget.org/packages/Auth0.AspNetCore.Authentication/) nuget package.

## Routes

It contains the following routes:

- `/`
  - Homepage, will show current users claims and what DLCS roles would be assigned. If not logged in will redirect to login.
- `/mappings`
  - Render Wellcome roles:Dlcs role mappings.
- `/account/login`
  - Log user in to application via Auth0.
- `/account/logout`
  - Log user out of application + auth0.

## Config

The following configuration values will need to be added to run the app:

```json
"Auth0": {
    "Domain": "<my-domain>.eu.auth0.com",
    "ClientId": "<client-id>"
  }
```

## Deploying

Run `build.sh` and pass account number to build + push docker image and restart ECS service.