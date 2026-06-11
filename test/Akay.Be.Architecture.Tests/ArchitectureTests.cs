using System.Reflection;
using NetArchTest.Rules;

namespace Akay.Be.Architecture.Tests;

public sealed class ArchitectureTests
{

    [Fact]
    public void ApplicationShouldNotDependOnOuterLayers()
    {
        NetArchTest.Rules.TestResult result = Types.InAssembly(Assembly.Load("Akay.Be.Application"))
            .ShouldNot()
            .HaveDependencyOnAny("Akay.Be.Infrastructure", "Akay.Be.Host")
            .GetResult();

        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public void InfrastructureShouldNotDependOnHost()
    {
        NetArchTest.Rules.TestResult result = Types.InAssembly(Assembly.Load("Akay.Be.Infrastructure"))
            .ShouldNot()
            .HaveDependencyOnAny("Akay.Be.Host")
            .GetResult();

        Assert.True(result.IsSuccessful);
    }



    [Fact]
    public void CreateClient_Must_Use_HttpClientNames_Constants()
    {
        var srcDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "src"));
        var violations = new List<string>();

        foreach (var file in Directory.EnumerateFiles(srcDir, "*.cs", SearchOption.AllDirectories))
        {
            var fileName = Path.GetFileName(file);
            if (fileName == "HttpClientNames.cs")
                continue;

            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.Contains("CreateClient(\"", StringComparison.Ordinal) && !line.Contains("HttpClientNames", StringComparison.Ordinal))
                {
                    violations.Add($"{file}:{i + 1} -> {line.Trim()}");
                }
            }
        }

        Assert.Empty(violations);
    }
}
