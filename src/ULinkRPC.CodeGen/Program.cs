using System.Text;
using System.Text.RegularExpressions;

namespace ULinkRPC.CodeGen;

internal static class Program
{
    private const string ContractsRelativePath = "samples/RpcCall/RpcCall.Unity/Packages/com.bruce.rpc.contracts";
    private const string UnityOutputRelativePath = "samples/RpcCall/RpcCall.Unity/Assets/Scripts/Rpc/RpcGenerated";
    private const string ServerOutputRelativePath = "samples/RpcCall/RpcCall.Server/Generated";
    private const string DefaultOutputDirName = "RpcGenerated";
    private const string DefaultRuntimeNamespace = "Game.Rpc.Runtime.Generated";
    private const string DefaultClientRuntimeUsing = "Game.Rpc.Runtime";
    private const string DefaultServerRuntimeUsing = "ULinkRPC.Runtime";

    private static int Main(string[] args)
    {
        if (args.Length > 0 && (args[0] == "-h" || args[0] == "--help"))
        {
            PrintUsage();
            return 0;
        }

        if (!TryResolvePaths(
                args,
                out var contractsPath,
                out var outputPath,
                out var binderOutputPath,
                out var serverOutputPath,
                out var serverNamespace,
                out var mode,
                out var outputSpecified,
                out var serverOutputSpecified,
                out var error))
        {
            Console.Error.WriteLine(error);
            PrintUsage();
            return 1;
        }

        var services = FindRpcServicesFromSource(contractsPath);
        if (services.Count == 0)
        {
            Console.Error.WriteLine("No [RpcService] interfaces found.");
            return 1;
        }

        if (mode == OutputMode.Unity || outputSpecified)
        {
            Directory.CreateDirectory(outputPath);
            Directory.CreateDirectory(binderOutputPath);
        }

        if (mode == OutputMode.Server || serverOutputSpecified)
        {
            if (string.IsNullOrWhiteSpace(serverNamespace))
                serverNamespace = GetDefaultServerNamespace(services);

            Directory.CreateDirectory(serverOutputPath);
        }

        var generated = 0;
        foreach (var svc in services)
        {
            if (mode == OutputMode.Unity || outputSpecified)
            {
                var (client, binder) = GenerateCode(svc, DefaultRuntimeNamespace, DefaultClientRuntimeUsing);
                if (client != null)
                {
                    var clientTypeName = GetClientTypeName(svc.InterfaceName);
                    File.WriteAllText(Path.Combine(outputPath, $"{clientTypeName}.cs"), client, Encoding.UTF8);
                    generated++;
                }

                if (binder != null)
                {
                    var binderTypeName = GetBinderTypeName(svc.InterfaceName);
                    File.WriteAllText(Path.Combine(binderOutputPath, $"{binderTypeName}.cs"), binder, Encoding.UTF8);
                    generated++;
                }
            }

            if (mode == OutputMode.Server || serverOutputSpecified)
            {
                var serverBinder = GenerateBinderCode(svc, serverNamespace, DefaultServerRuntimeUsing);
                var binderTypeName = GetBinderTypeName(svc.InterfaceName);
                File.WriteAllText(Path.Combine(serverOutputPath, $"{binderTypeName}.cs"), serverBinder, Encoding.UTF8);
                generated++;
            }
        }

        if (mode == OutputMode.Server || serverOutputSpecified)
        {
            var allBinder = GenerateAllServicesBinder(services, serverNamespace, DefaultServerRuntimeUsing);
            File.WriteAllText(Path.Combine(serverOutputPath, "AllServicesBinder.cs"), allBinder, Encoding.UTF8);
            generated++;
        }

        Console.WriteLine($"Generated {generated} files for {services.Count} service(s).");
        return 0;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("ULinkRPC.CodeGen usage:");
        Console.WriteLine("  ulinkrpc-codegen [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --contracts <path>      Path to contract sources");
        Console.WriteLine("  --output <path>         Output directory for generated clients (Unity)");
        Console.WriteLine("  --binder-output <path>  Output directory for generated binders (Unity)");
        Console.WriteLine("  --server-output <path>  Output directory for server binders");
        Console.WriteLine("  --server-namespace <ns> Namespace for server binders");
        Console.WriteLine("  --mode <auto|unity|server>  Force output mode");
        Console.WriteLine();
        Console.WriteLine("Defaults:");
        Console.WriteLine("  auto: Unity project generates Unity output; Server project generates server binders.");
        Console.WriteLine("  Paths can be overridden via options.");
    }

    private static bool TryResolvePaths(
        string[] args,
        out string contractsPath,
        out string outputPath,
        out string binderOutputPath,
        out string serverOutputPath,
        out string serverNamespace,
        out OutputMode mode,
        out bool outputSpecified,
        out bool serverOutputSpecified,
        out string error)
    {
        contractsPath = string.Empty;
        outputPath = string.Empty;
        binderOutputPath = string.Empty;
        serverOutputPath = string.Empty;
        serverNamespace = string.Empty;
        mode = OutputMode.Auto;
        outputSpecified = false;
        serverOutputSpecified = false;
        error = string.Empty;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg == "--contracts" && i + 1 < args.Length)
            {
                contractsPath = args[++i];
            }
            else if (arg == "--output" && i + 1 < args.Length)
            {
                outputPath = args[++i];
                outputSpecified = true;
            }
            else if (arg == "--binder-output" && i + 1 < args.Length)
            {
                binderOutputPath = args[++i];
                outputSpecified = true;
            }
            else if (arg == "--server-output" && i + 1 < args.Length)
            {
                serverOutputPath = args[++i];
                serverOutputSpecified = true;
            }
            else if (arg == "--server-namespace" && i + 1 < args.Length)
            {
                serverNamespace = args[++i];
            }
            else if (arg == "--mode" && i + 1 < args.Length)
            {
                var value = args[++i];
                if (!TryParseMode(value, out mode))
                {
                    error = $"Unknown mode: {value}";
                    return false;
                }
            }
            else
            {
                error = $"Unknown or incomplete option: {arg}";
                return false;
            }
        }

        var cwd = Directory.GetCurrentDirectory();
        var repoRoot = FindRepoRoot(cwd);
        var isInRepo = repoRoot != null;

        if (string.IsNullOrWhiteSpace(contractsPath))
        {
            if (!isInRepo)
            {
                error = "Contracts path not provided and repo root not found. Use --contracts <path>.";
                return false;
            }
            contractsPath = Path.Combine(repoRoot!, ContractsRelativePath);
        }
        else
        {
            contractsPath = Path.GetFullPath(contractsPath);
        }

        if (mode == OutputMode.Auto)
        {
            mode = DetectMode(repoRoot, contractsPath);
        }

        if (mode == OutputMode.Unity || outputSpecified)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                outputPath = isInRepo
                    ? Path.Combine(repoRoot!, UnityOutputRelativePath)
                    : Path.Combine(cwd, DefaultOutputDirName);
            }
            else
            {
                outputPath = Path.GetFullPath(outputPath);
            }

            if (string.IsNullOrWhiteSpace(binderOutputPath))
            {
                binderOutputPath = outputPath;
            }
            else
            {
                binderOutputPath = Path.GetFullPath(binderOutputPath);
            }

            if (!string.Equals(outputPath, binderOutputPath, StringComparison.OrdinalIgnoreCase))
            {
                error = "--binder-output must match --output (single output directory).";
                return false;
            }
        }

        if (mode == OutputMode.Server || serverOutputSpecified)
        {
            if (string.IsNullOrWhiteSpace(serverOutputPath))
            {
                if (isInRepo)
                    serverOutputPath = Path.Combine(repoRoot!, ServerOutputRelativePath);
                else
                    serverOutputPath = Path.Combine(cwd, "Generated");
            }
            else
            {
                serverOutputPath = Path.GetFullPath(serverOutputPath);
            }
        }

        if (!Directory.Exists(contractsPath))
        {
            error = $"Contracts path not found: {contractsPath}";
            return false;
        }

        return true;
    }

    private static string? FindRepoRoot(string startPath)
    {
        var dir = new DirectoryInfo(startPath);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, ContractsRelativePath);
            if (Directory.Exists(candidate))
                return dir.FullName;

            dir = dir.Parent;
        }

        return null;
    }

    private static OutputMode DetectMode(string? repoRoot, string contractsPath)
    {
        if (!string.IsNullOrWhiteSpace(repoRoot))
        {
            var unityAssets = Path.Combine(repoRoot, "samples", "RpcCall", "RpcCall.Unity", "Assets");
            var unityPackages = Path.Combine(repoRoot, "samples", "RpcCall", "RpcCall.Unity", "Packages");
            if (Directory.Exists(unityAssets) && Directory.Exists(unityPackages))
                return OutputMode.Unity;

            var serverDir = Path.Combine(repoRoot, "samples", "RpcCall", "RpcCall.Server", "Game.Rpc.Server");
            if (Directory.Exists(serverDir))
                return OutputMode.Server;
        }

        var cwd = Directory.GetCurrentDirectory();
        if (Directory.Exists(Path.Combine(cwd, "Assets")) && Directory.Exists(Path.Combine(cwd, "Packages")))
            return OutputMode.Unity;

        if (Directory.Exists(Path.Combine(cwd, "Game.Rpc.Server")))
            return OutputMode.Server;

        if (Directory.Exists(Path.Combine(contractsPath, "..", "Assets")))
            return OutputMode.Unity;

        return OutputMode.Server;
    }

    private static bool TryParseMode(string value, out OutputMode mode)
    {
        switch (value.ToLowerInvariant())
        {
            case "auto":
                mode = OutputMode.Auto;
                return true;
            case "unity":
                mode = OutputMode.Unity;
                return true;
            case "server":
                mode = OutputMode.Server;
                return true;
            default:
                mode = OutputMode.Auto;
                return false;
        }
    }

    private static List<RpcServiceInfo> FindRpcServicesFromSource(string contractsPath)
    {
        var files = Directory.GetFiles(contractsPath, "*.cs", SearchOption.AllDirectories);
        var services = new List<RpcServiceInfo>();

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            var ns = ParseNamespace(text);
            services.AddRange(ParseServices(text, ns));
        }

        return services;
    }

    private static string ParseNamespace(string text)
    {
        var match = Regex.Match(text, @"^\s*namespace\s+([A-Za-z0-9_.]+)\s*$", RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private static IEnumerable<RpcServiceInfo> ParseServices(string text, string ns)
    {
        var svcRegex = new Regex(@"\[RpcService\((\d+)\)\]\s*public\s+interface\s+(\w+)\s*{", RegexOptions.Multiline);
        var matches = svcRegex.Matches(text);

        foreach (Match match in matches)
        {
            var serviceId = int.Parse(match.Groups[1].Value);
            var ifaceName = match.Groups[2].Value;
            var ifaceFullName = string.IsNullOrEmpty(ns) ? ifaceName : $"{ns}.{ifaceName}";

            var braceIndex = text.IndexOf('{', match.Index);
            if (braceIndex < 0)
                continue;

            var endIndex = FindMatchingBrace(text, braceIndex);
            if (endIndex < 0)
                continue;

            var body = text.Substring(braceIndex + 1, endIndex - braceIndex - 1);
            var methods = ParseMethods(body);
            if (methods.Count > 0)
                yield return new RpcServiceInfo(ifaceName, ifaceFullName, serviceId, methods);
        }
    }

    private static int FindMatchingBrace(string text, int startIndex)
    {
        var depth = 0;
        for (var i = startIndex; i < text.Length; i++)
        {
            if (text[i] == '{')
                depth++;
            else if (text[i] == '}')
            {
                depth--;
                if (depth == 0)
                    return i;
            }
        }
        return -1;
    }

    private static List<RpcMethodInfo> ParseMethods(string body)
    {
        var methodRegex = new Regex(@"\[RpcMethod\((\d+)\)\]\s*([^\r\n;]+);", RegexOptions.Multiline);
        var matches = methodRegex.Matches(body);
        var methods = new List<RpcMethodInfo>();

        foreach (Match match in matches)
        {
            var methodId = int.Parse(match.Groups[1].Value);
            var signature = match.Groups[2].Value.Trim();
            if (!TryParseMethodSignature(signature, out var name, out var argType, out var retType, out var isVoid))
                continue;

            methods.Add(new RpcMethodInfo(name, methodId, argType, retType, isVoid));
        }

        return methods;
    }

    private static bool TryParseMethodSignature(
        string signature,
        out string name,
        out string? argType,
        out string? retType,
        out bool isVoid)
    {
        name = string.Empty;
        argType = null;
        retType = null;
        isVoid = false;

        var match = Regex.Match(signature, @"^\s*(?<ret>[^\s]+)\s+(?<name>\w+)\s*\((?<params>[^\)]*)\)\s*$");
        if (!match.Success)
            return false;

        var ret = match.Groups["ret"].Value.Trim();
        name = match.Groups["name"].Value.Trim();
        var paramList = match.Groups["params"].Value.Trim();

        if (string.Equals(ret, "ValueTask", StringComparison.Ordinal) ||
            string.Equals(ret, "System.Threading.Tasks.ValueTask", StringComparison.Ordinal))
        {
            isVoid = true;
        }
        else
        {
            var genericMatch = Regex.Match(ret, @"^(?:System\.Threading\.Tasks\.)?ValueTask<(.+)>$");
            if (genericMatch.Success)
                retType = genericMatch.Groups[1].Value.Trim();
            else
                retType = ret;
        }

        if (!string.IsNullOrWhiteSpace(paramList))
        {
            var param = paramList.Split(',')[0];
            var paramCore = param.Split('=')[0].Trim();
            var tokens = paramCore.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length >= 2)
            {
                var typeToken = tokens[0];
                if (typeToken is "in" or "ref" or "out")
                {
                    if (tokens.Length >= 3)
                        typeToken = tokens[1];
                }
                argType = typeToken;
            }
        }

        return true;
    }

    private static (string? Client, string? Binder) GenerateCode(
        RpcServiceInfo svc,
        string runtimeNamespace,
        string runtimeUsing)
    {
        var ifaceName = svc.InterfaceName;
        var clientTypeName = GetClientTypeName(ifaceName);

        var clientBody = new StringBuilder();
        clientBody.Append("using System.Threading;\nusing System.Threading.Tasks;\nusing Game.Rpc.Contracts;\nusing Game.Rpc.Runtime;\n\nnamespace ")
            .Append(runtimeNamespace)
            .Append("\n{\n");
        clientBody.Append("    public sealed class ").Append(clientTypeName).Append(" : ").Append(ifaceName).Append("\n    {\n");
        clientBody.Append("        private const int ServiceId = ").Append(svc.ServiceId).Append(";\n");
        clientBody.Append("        private readonly RpcClient _client;\n\n");
        clientBody.Append("        public ").Append(clientTypeName).Append("(RpcClient client) { _client = client; }\n\n");

        foreach (var m in svc.Methods)
        {
            var hasArg = !string.IsNullOrEmpty(m.ArgTypeName);
            var argType = hasArg ? m.ArgTypeName! : "RpcVoid";
            var retType = m.IsVoid ? "RpcVoid" : m.RetTypeName!;
            var argVal = hasArg ? "req" : "RpcVoid.Instance";
            var sig = hasArg ? $"{m.Name}({argType} req)" : $"{m.Name}()";
            if (m.IsVoid)
            {
                clientBody.Append("        public async ValueTask ").Append(sig).Append("\n        {\n");
                clientBody.Append("            await _client.CallAsync<RpcVoid, RpcVoid>(ServiceId, ").Append(m.MethodId).Append(", RpcVoid.Instance, CancellationToken.None);\n        }\n\n");
            }
            else
            {
                clientBody.Append("        public async ValueTask<").Append(retType).Append("> ").Append(sig).Append("\n        {\n");
                clientBody.Append("            return await _client.CallAsync<").Append(argType).Append(", ").Append(retType).Append(">(ServiceId, ")
                    .Append(m.MethodId).Append(", ").Append(argVal).Append(", CancellationToken.None);\n        }\n\n");
            }
        }
        clientBody.Append("    }\n}\n");

        return (clientBody.ToString(), GenerateBinderCode(svc, runtimeNamespace, runtimeUsing));
    }

    private static string GenerateBinderCode(RpcServiceInfo svc, string ns, string runtimeUsing)
    {
        var ifaceName = svc.InterfaceName;
        var binderTypeName = GetBinderTypeName(ifaceName);
        var binderSb = new StringBuilder();
        binderSb.Append("using System;\nusing Game.Rpc.Contracts;\nusing ")
            .Append(runtimeUsing)
            .Append(";\n\nnamespace ")
            .Append(ns)
            .Append("\n{\n");
        binderSb.Append("    public static class ").Append(binderTypeName).Append("\n    {\n");
        binderSb.Append("        private const int ServiceId = ").Append(svc.ServiceId).Append(";\n\n");
        binderSb.Append("        public static void Bind(RpcServer server, ").Append(ifaceName).Append(" impl)\n        {\n");

        foreach (var m in svc.Methods)
        {
            var hasArg = !string.IsNullOrEmpty(m.ArgTypeName);
            var argType = hasArg ? m.ArgTypeName! : "RpcVoid";
            var retType = m.IsVoid ? "RpcVoid" : m.RetTypeName!;
            binderSb.Append("            server.Register(ServiceId, ").Append(m.MethodId).Append(", async (req, ct) =>\n            {\n");
            if (hasArg)
                binderSb.Append("                var arg = server.Serializer.Deserialize<").Append(argType).Append(">(req.Payload.AsSpan())!;\n");
            if (m.IsVoid)
            {
                if (hasArg)
                    binderSb.Append("                await impl.").Append(m.Name).Append("(arg);\n");
                else
                    binderSb.Append("                await impl.").Append(m.Name).Append("();\n");
                binderSb.Append("                return new RpcResponseEnvelope { RequestId = req.RequestId, Status = RpcStatus.Ok, Payload = server.Serializer.Serialize(RpcVoid.Instance) };\n");
            }
            else
            {
                if (hasArg)
                    binderSb.Append("                var resp = await impl.").Append(m.Name).Append("(arg);\n");
                else
                    binderSb.Append("                var resp = await impl.").Append(m.Name).Append("();\n");
                binderSb.Append("                return new RpcResponseEnvelope { RequestId = req.RequestId, Status = RpcStatus.Ok, Payload = server.Serializer.Serialize(resp) };\n");
            }
            binderSb.Append("            });\n\n");
        }
        binderSb.Append("        }\n    }\n}\n");

        return binderSb.ToString();
    }

    private static string GenerateAllServicesBinder(List<RpcServiceInfo> services, string ns, string runtimeUsing)
    {
        var sb = new StringBuilder();
        sb.Append("using Game.Rpc.Contracts;\nusing ")
            .Append(runtimeUsing)
            .Append(";\n\nnamespace ")
            .Append(ns)
            .Append("\n{\n");
        sb.Append("    public static class AllServicesBinder\n    {\n");
        sb.Append("        public static void BindAll(RpcServer server");
        foreach (var svc in services)
        {
            sb.Append(", ").Append(svc.InterfaceName).Append(" ").Append(GetServiceParamName(svc.InterfaceName));
        }
        sb.Append(")\n        {\n");
        foreach (var svc in services)
        {
            sb.Append("            ").Append(GetBinderTypeName(svc.InterfaceName))
                .Append(".Bind(server, ").Append(GetServiceParamName(svc.InterfaceName)).Append(");\n");
        }
        sb.Append("        }\n    }\n}\n");
        return sb.ToString();
    }

    private static string GetServiceParamName(string ifaceName)
    {
        var baseName = GetServiceTypeName(ifaceName);
        if (baseName.Length == 0)
            return "service";
        return char.ToLowerInvariant(baseName[0]) + baseName.Substring(1);
    }

    private static string GetDefaultServerNamespace(List<RpcServiceInfo> services)
    {
        var first = services.FirstOrDefault();
        if (first == null)
            return "ULinkRPC.Server.Generated";

        var ns = first.InterfaceFullName;
        var lastDot = ns.LastIndexOf('.');
        var baseNs = lastDot > 0 ? ns.Substring(0, lastDot) : ns;
        if (baseNs.EndsWith(".Contracts", StringComparison.Ordinal))
            baseNs = baseNs.Substring(0, baseNs.Length - ".Contracts".Length);

        if (string.IsNullOrWhiteSpace(baseNs))
            return "ULinkRPC.Server.Generated";

        return $"{baseNs}.Server.Generated";
    }

    private static string GetBinderTypeName(string ifaceName)
    {
        return $"{GetServiceTypeName(ifaceName)}Binder";
    }

    private static string GetClientTypeName(string ifaceName)
    {
        return $"{GetServiceTypeName(ifaceName)}Client";
    }

    private static string GetServiceTypeName(string ifaceName)
    {
        if (ifaceName.Length > 1 && ifaceName[0] == 'I' && char.IsUpper(ifaceName[1]))
            return ifaceName.Substring(1);

        return ifaceName;
    }

    private sealed class RpcServiceInfo
    {
        public string InterfaceName { get; }
        public string InterfaceFullName { get; }
        public int ServiceId { get; }
        public List<RpcMethodInfo> Methods { get; }

        public RpcServiceInfo(string interfaceName, string interfaceFullName, int serviceId, List<RpcMethodInfo> methods)
        {
            InterfaceName = interfaceName;
            InterfaceFullName = interfaceFullName;
            ServiceId = serviceId;
            Methods = methods;
        }
    }

    private sealed class RpcMethodInfo
    {
        public string Name { get; }
        public int MethodId { get; }
        public string? ArgTypeName { get; }
        public string? RetTypeName { get; }
        public bool IsVoid { get; }

        public RpcMethodInfo(string name, int methodId, string? argTypeName, string? retTypeName, bool isVoid)
        {
            Name = name;
            MethodId = methodId;
            ArgTypeName = argTypeName;
            RetTypeName = retTypeName;
            IsVoid = isVoid;
        }
    }

    private enum OutputMode
    {
        Auto,
        Unity,
        Server
    }
}
