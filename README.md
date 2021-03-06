# AWS ASPNETCORE Environment Startup

I couldn't find anything online with regards to configuring ASPNETCORE apps in AWS EC2 so I've shared the way I go about it.

## Requirements:

* No environmental config sitting on the server(s)
  * This allows a new environment to be setup in AWS without any code changes

* No secrets checked into source control or sitting on the server(s) (apart from Local Development config)

## Boot Flow

1. Check InstanceId.
   * If it's NULL, we're running locally (LocalDevelopment) - exit
   * If it exists, retrieve the value of an EC2 "tag" named "environment"
2. Use the environment name to retrieve secure config from the AWS Parameter Store (the names of the parameters should match the paths in appsettings.json)
3. On Startup, set the environment name and overwrite any local config values with the config retrieved from the server

```C#
public static IWebHost BuildWebHost()
{
    // ===================================
    // Get the boot config from the server
    // ===================================
    var bootConfig = Task.Run(() => BootHelper.GetConfig()).Result;

    var webHost = new WebHostBuilder()
        .UseKestrel()
        .UseContentRoot(Directory.GetCurrentDirectory())
        .ConfigureAppConfiguration((context, config) =>
        {
            // !!! IMPORTANT !!!
            // Set the environment from boot config
            context.HostingEnvironment.EnvironmentName = bootConfig.Environment;

            config.AddJsonFile("appsettings.json", optional: true)
                    .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true);

            // !!! IMPORTANT !!!
            // If there are any parameters from the server
            // then we'll use them to override anything in the JSON files
            config.AddInMemoryCollection(bootConfig.Parameters);
        })
        .UseIISIntegration()
        .UseStartup<Startup>()
        .Build();

    return webHost;
}
```

In order to stop your LocalDevelopment json file being published to the server when using WebDeploy, you can use the following in the csproj:

```xml
<ItemGroup>
    <Content Update="appsettings.LocalDevelopment.json" CopyToPublishDirectory="Never" />
</ItemGroup>
```

## Policies required by IAM Role to access tags and parameters

```
{
  "Version": "2012-10-17",
  "Statement": [
      {
          "Sid": "VisualEditor0",
          "Effect": "Allow",
          "Action": [
              "ec2:DescribeInstances",
              "tag:GetResources",
              "tag:GetTagValues",
              "tag:GetTagKeys"
          ],
          "Resource": "*"
      }
  ]
}


{
  "Version": "2012-10-17",
  "Statement": [
      {
          "Sid": "VisualEditor0",
          "Effect": "Allow",
          "Action": "ssm:GetParametersByPath",
          "Resource": [
              "arn:aws:ssm:YOUR_ARN_HERE:parameter/development",
              "arn:aws:ssm:YOUR_ARN_HERE:parameter/development/*"
          ]
      }
  ]
}
```
