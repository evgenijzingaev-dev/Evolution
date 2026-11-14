"""Compile model/test sources with Unity-like assembly boundaries, without Unity stubs."""
from pathlib import Path
import subprocess
import tempfile

source = Path(__file__).resolve().parents[1] / "Assets/Evolution"
with tempfile.TemporaryDirectory(prefix="evolution-assemblies-") as temporary:
    root = Path(temporary)
    for name, references in [("Core", []), ("Presentation", ["Core"]),
                             ("Volume", ["Core"]), ("Localization", [])]:
        folder = root / name
        folder.mkdir()
        refs = "".join(f'<ProjectReference Include="../{r}/{r}.csproj" />' for r in references)
        (folder / f"{name}.csproj").write_text(
            '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net8.0</TargetFramework>'
            f'<AssemblyName>Evolution.{name}</AssemblyName><LangVersion>9.0</LangVersion>'
            f'</PropertyGroup><ItemGroup><Compile Include="{source / name}/*.cs" />{refs}</ItemGroup></Project>')
    folder = root / "Tests"
    folder.mkdir()
    refs = "".join(f'<ProjectReference Include="../{r}/{r}.csproj" />'
                   for r in ["Core", "Volume", "Presentation", "Localization"])
    project = folder / "Tests.csproj"
    project.write_text(
        '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net8.0</TargetFramework>'
        '<AssemblyName>Evolution.Tests</AssemblyName><LangVersion>9.0</LangVersion></PropertyGroup>'
        '<ItemGroup><PackageReference Include="NUnit" Version="3.14.0" />'
        f'<Compile Include="{source}/Tests/EditMode/*.cs" '
        f'Exclude="{source}/Tests/EditMode/FriendlyOceanHudInputTests.cs" />{refs}</ItemGroup></Project>')
    subprocess.run(["dotnet", "build", str(project), "-m:1", "--verbosity", "quiet"], check=True)
print("Separate model/test assemblies PASS; Unity Runtime/API compilation remains unverified.")
