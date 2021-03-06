#tool "nuget:?package=xunit.runner.console"
#tool "nuget:?package=OpenCover"

var configuration = Argument("configuration", "Release");
var version = Argument<string>("buildVersion", null);
var target = Argument("target", "Default");

Task("Restore")
    .Does(()=> 
{
    DotNetCoreRestore();
});

Task("Clean")
    .Does(()=> 
{
    CleanDirectory("./build");
    StartProcess("dotnet", "clean -c:" + configuration);
});

Task("SpecifyPackageVersion")
    .WithCriteria(AppVeyor.IsRunningOnAppVeyor)
    .Does(() => 
{
    version = AppVeyor.Environment.Build.Version;

    if (AppVeyor.Environment.Repository.Tag.IsTag)
    {
        var tagName = AppVeyor.Environment.Repository.Tag.Name;
        if(tagName.StartsWith("v"))
        {
            version = tagName.Substring(1);
        }

        AppVeyor.UpdateBuildVersion(version);
    }
});

Task("Build")
    .IsDependentOn("SpecifyPackageVersion")
    .IsDependentOn("Clean")
    .IsDependentOn("Restore")
    .Does(()=> 
{
    var buildSettings =  new DotNetCoreBuildSettings { Configuration = configuration };
    if(!string.IsNullOrEmpty(version)) buildSettings.ArgumentCustomization = args => args.Append("/p:Version=" + version);

    DotNetCoreBuild("src/Cronos/Cronos.csproj",  buildSettings);
});

Task("Test")
    .IsDependentOn("Build")
    .Does(() =>
{
    DotNetCoreTest("./tests/Cronos.Tests/Cronos.Tests.csproj", new DotNetCoreTestSettings
    {
        Configuration = "Release",
        ArgumentCustomization = args => args.Append("/p:BuildProjectReferences=false")
    });
});

Task("OpenCover")
    .IsDependentOn("Test")
    .Does(() => 
{
    OpenCover(
        tool => { tool.XUnit2("tests/Cronos.Tests/bin/" + configuration + "/**/Cronos.Tests.dll", new XUnit2Settings { ShadowCopy = false }); },
        new FilePath("coverage.xml"),
        new OpenCoverSettings()
            .WithFilter("+[Cronos]*")
            .WithFilter("-[Cronos.Tests]*"));
});

Task("Pack")
    .IsDependentOn("OpenCover")
    .Does(()=> 
{
    CreateDirectory("build");
    
    CopyFiles(GetFiles("./src/Cronos/bin/**/*.nupkg"), "build");
    Zip("./src/Cronos/bin/" + configuration, "build/Cronos-" + version +".zip");
});

Task("Default")
    .IsDependentOn("Pack");

Task("CI")
    .IsDependentOn("Pack");
    
RunTarget(target);