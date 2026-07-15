var builder = DistributedApplication.CreateBuilder(args);

# region Identity
var keycloak = builder.AddLocalKeycloak("keycloak");
# endregion

builder.Build().Run();
