using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;


namespace UpdateFileVersionOnTag
{

    public class Version
    {
        public int Major { get; private set; }
        public int Minor { get; private set; }
        public int Build { get; private set; }
        public string? Label { get; private set; }
        public int? Revision { get; private set; }

        private static readonly Regex versionRegex = new Regex(@"^H2M-v(?<major>\d+)\.(?<minor>\d+)\.(?<build>\d+)(?:-(?<label>[a-zA-Z]+)(?:\.(?<revision>\d+))?)?$");

        public Version(int major, int minor, int build, string? label, int? revision)
        {
            Major = major;
            Minor = minor;
            Build = build;
            Label = label;
            Revision = revision;
        }

        public static Version Parse(string versionString)
        {
            // Possible format:
            //  H2M-v0.0.0
            //  H2M-v0.0.0-beta
            //  H2M-v0.0.0-beta.1

            var match = versionRegex.Match(versionString);
            if (!match.Success)
            {
                throw new FormatException("Version format is invalid.");
            }

            return new Version(
                int.Parse(match.Groups["major"].Value),
                int.Parse(match.Groups["minor"].Value),
                int.Parse(match.Groups["build"].Value),
                match.Groups["label"].Success ? match.Groups["label"].Value : null,
                match.Groups["revision"].Success ? int.Parse(match.Groups["revision"].Value) : null
            );

        }

        public override bool Equals(object? obj)
        {
            if (obj is not Version other) return false;

            return Major == other.Major &&
                   Minor == other.Minor &&
                   Build == other.Build &&
                   Label == other.Label &&
                   Revision == other.Revision;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Major, Minor, Build, Label, Revision);
        }
    }

    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                throw new ArgumentException("Expected exactly two arguments: <tagVersion> <branchName>.");
            }

            string versionString = args[0];
            string branchName = args[1];

            Version version = Version.Parse(versionString);

            ModifyAssemblyInfo(version);
            ModifyLauncherService(version, branchName);
        }

        static void ModifyAssemblyInfo(Version version)
        {
            string h2mLauncherPath = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).Parent.Parent.Parent.Parent.FullName;
            string assemblyInfoPath = Path.Combine(h2mLauncherPath, "H2MLauncher.UI", "Properties", "AssemblyInfo.cs");

            var code = File.ReadAllText(assemblyInfoPath);
            var tree = CSharpSyntaxTree.ParseText(code);

            string versionString = $"{version.Major}.{version.Minor}.{version.Build}." +
                                    (version.Revision is null ? "0" : $"{version.Revision}");

            var root = tree.GetRoot();
            var newRoot = root.ReplaceNodes(
                root.DescendantNodes().OfType<AttributeSyntax>(),
                (oldNode, newNode) =>
                {
                    var attributeName = oldNode.Name.ToString();

                    if (attributeName.Contains("AssemblyVersion") || attributeName.Contains("AssemblyFileVersion"))
                    {
                        return SyntaxFactory.Attribute(SyntaxFactory.ParseName(attributeName))
                            .WithArgumentList(SyntaxFactory.AttributeArgumentList(
                                SyntaxFactory.SingletonSeparatedList(SyntaxFactory.AttributeArgument(
                                    SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression,
                                        SyntaxFactory.Literal(versionString))))));
                    }

                    return newNode;
                });

            File.WriteAllText(assemblyInfoPath, newRoot.ToFullString());
        }

        static void ModifyLauncherService(Version version, string branchName)
        {
            string h2mLauncherPath = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).Parent.Parent.Parent.Parent.FullName;
            string launcherServicePath = Path.Combine(h2mLauncherPath, "H2MLauncher.Core", "Services", "LauncherService.cs");

            var code = File.ReadAllText(launcherServicePath);
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetRoot();

            // Helper method to replace a variable's literal value
            void ReplaceVariableValue(string variableName, string newValue)
            {
                var variableDeclarator = root.DescendantNodes()
                    .OfType<VariableDeclaratorSyntax>()
                    .FirstOrDefault(v => v.Identifier.Text == variableName);

                if (variableDeclarator?.Initializer?.Value is not LiteralExpressionSyntax oldValue)
                {
                    throw new Exception($"Variable '{variableName}' not found.");
                }

                var newLiteral = SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(newValue));
                root = root.ReplaceNode(oldValue, newLiteral);
            }

            ReplaceVariableValue("CurrentPreReleaseLabel", version.Label ?? "");
            ReplaceVariableValue("CurrentBranch", branchName);

            File.WriteAllText(launcherServicePath, root.ToFullString());
        }
    }
}
