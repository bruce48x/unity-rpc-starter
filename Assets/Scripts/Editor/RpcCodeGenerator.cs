using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Game.Rpc.Contracts;
using UnityEditor;
using UnityEngine;

namespace Game.Rpc.Editor
{
    public static class RpcCodeGenerator
    {
        private const string OutputPath = "Assets/Scripts/Rpc/Generated";
        public static bool IsGenerating { get; private set; }

        [MenuItem("Tools/RPC/Generate RPC Code")]
        public static void Generate()
        {
            GenerateInternal(refreshAssetDatabase: true, logResult: true);
        }

        public static void GenerateAuto()
        {
            GenerateInternal(refreshAssetDatabase: true, logResult: false);
        }

        private static void GenerateInternal(bool refreshAssetDatabase, bool logResult)
        {
            try
            {
                if (IsGenerating)
                    return;

                IsGenerating = true;
                var services = FindRpcServices();
                if (services.Count == 0)
                {
                    if (logResult)
                        Debug.LogWarning("[RpcCodeGenerator] No [RpcService] interfaces found.");
                    return;
                }

                EnsureOutputDirectory();

                int generated = 0;
                foreach (var svc in services)
                {
                    var (client, binder) = GenerateCode(svc);
                    if (client != null)
                    {
                        WriteFile($"{svc.InterfaceName}Client.cs", client);
                        generated++;
                    }
                    if (binder != null)
                    {
                        WriteFile($"{svc.InterfaceName}Binder.cs", binder);
                        generated++;
                    }
                }

                if (refreshAssetDatabase)
                    AssetDatabase.Refresh();

                if (logResult)
                    Debug.Log($"[RpcCodeGenerator] Generated {generated} files for {services.Count} service(s).");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RpcCodeGenerator] Error: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                IsGenerating = false;
            }
        }

        private static List<RpcServiceInfo> FindRpcServices()
        {
            var services = new List<RpcServiceInfo>();
            var rpcServiceAttrType = typeof(RpcServiceAttribute);
            var rpcMethodAttrType = typeof(RpcMethodAttribute);

            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in asm.GetTypes())
                    {
                        if (!type.IsInterface)
                            continue;

                        var serviceAttr = type.GetCustomAttribute(rpcServiceAttrType);
                        if (serviceAttr == null)
                            continue;

                        var serviceId = ((RpcServiceAttribute)serviceAttr).ServiceId;
                        var methods = new List<RpcMethodInfo>();

                        foreach (var method in type.GetMethods())
                        {
                            var methodAttr = method.GetCustomAttribute(rpcMethodAttrType);
                            if (methodAttr == null)
                                continue;

                            var methodId = ((RpcMethodAttribute)methodAttr).MethodId;
                            var parameters = method.GetParameters();
                            var returnType = method.ReturnType;

                            Type? argType = null;
                            if (parameters.Length == 1)
                                argType = parameters[0].ParameterType;
                            else if (parameters.Length > 1)
                                continue; // invalid

                            bool isVoid = false;
                            Type? retType = null;

                            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition().Name == "ValueTask`1")
                            {
                                retType = returnType.GetGenericArguments()[0];
                            }
                            else if (returnType.Name == "ValueTask" && !returnType.IsGenericType)
                            {
                                isVoid = true;
                            }
                            else
                            {
                                retType = returnType;
                            }

                            methods.Add(new RpcMethodInfo(method.Name, methodId, argType, retType, isVoid));
                        }

                        if (methods.Count > 0)
                            services.Add(new RpcServiceInfo(type, serviceId, methods));
                    }
                }
                catch (ReflectionTypeLoadException)
                {
                    // Skip assemblies that can't be fully loaded
                }
            }

            return services;
        }

        private static (string? Client, string? Binder) GenerateCode(RpcServiceInfo svc)
        {
            var iface = svc.Interface;
            var ifaceName = iface.Name;

            var clientBody = new StringBuilder();
            clientBody.Append("using System.Threading;\nusing System.Threading.Tasks;\nusing Game.Rpc.Contracts;\nusing Game.Rpc.Runtime;\n\nnamespace Game.Rpc.Runtime.Generated\n{\n");
            clientBody.Append("    public sealed class ").Append(ifaceName).Append("Client : ").Append(ifaceName).Append("\n    {\n");
            clientBody.Append("        private const int ServiceId = ").Append(svc.ServiceId).Append(";\n");
            clientBody.Append("        private readonly RpcClient _client;\n\n");
            clientBody.Append("        public ").Append(ifaceName).Append("Client(RpcClient client) { _client = client; }\n\n");

            foreach (var m in svc.Methods)
            {
                var argType = m.ArgType == null ? "RpcVoid" : GetTypeName(m.ArgType);
                var retType = m.IsVoid ? "RpcVoid" : GetTypeName(m.RetType!);
                var argVal = m.ArgType == null ? "RpcVoid.Instance" : "req";
                var sig = m.ArgType == null ? $"{m.Name}()" : $"{m.Name}({argType} req)";
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

            var binderSb = new StringBuilder();
            binderSb.Append("using Game.Rpc.Contracts;\nusing Game.Rpc.Runtime;\nusing MemoryPack;\n\nnamespace Game.Rpc.Runtime.Generated\n{\n");
            binderSb.Append("    public static class ").Append(ifaceName).Append("Binder\n    {\n");
            binderSb.Append("        private const int ServiceId = ").Append(svc.ServiceId).Append(";\n\n");
            binderSb.Append("        public static void Bind(RpcServer server, ").Append(ifaceName).Append(" impl)\n        {\n");

            foreach (var m in svc.Methods)
            {
                var argType = m.ArgType == null ? "RpcVoid" : GetTypeName(m.ArgType);
                var retType = m.IsVoid ? "RpcVoid" : GetTypeName(m.RetType!);
                binderSb.Append("            server.Register(ServiceId, ").Append(m.MethodId).Append(", async (req, ct) =>\n            {\n");
                if (m.ArgType != null)
                    binderSb.Append("                var arg = MemoryPackSerializer.Deserialize<").Append(argType).Append(">(req.Payload)!;\n");
                if (m.IsVoid)
                {
                    if (m.ArgType != null)
                        binderSb.Append("                await impl.").Append(m.Name).Append("(arg);\n");
                    else
                        binderSb.Append("                await impl.").Append(m.Name).Append("();\n");
                    binderSb.Append("                return new RpcResponseEnvelope { RequestId = req.RequestId, Status = RpcStatus.Ok, Payload = MemoryPackSerializer.Serialize(RpcVoid.Instance) };\n");
                }
                else
                {
                    if (m.ArgType != null)
                        binderSb.Append("                var resp = await impl.").Append(m.Name).Append("(arg);\n");
                    else
                        binderSb.Append("                var resp = await impl.").Append(m.Name).Append("();\n");
                    binderSb.Append("                return new RpcResponseEnvelope { RequestId = req.RequestId, Status = RpcStatus.Ok, Payload = MemoryPackSerializer.Serialize(resp) };\n");
                }
                binderSb.Append("            });\n\n");
            }
            binderSb.Append("        }\n    }\n}\n");

            return (clientBody.ToString(), binderSb.ToString());
        }

        private static string GetTypeName(Type type)
        {
            if (type.IsGenericType)
            {
                var name = type.Name.Substring(0, type.Name.IndexOf('`'));
                var args = string.Join(", ", type.GetGenericArguments().Select(GetTypeName));
                var ns = type.Namespace;
                return string.IsNullOrEmpty(ns) ? $"{name}<{args}>" : $"{ns}.{name}<{args}>";
            }
            return type.FullName ?? type.Name;
        }

        private static void EnsureOutputDirectory()
        {
            if (!Directory.Exists(OutputPath))
                Directory.CreateDirectory(OutputPath);
        }

        private static void WriteFile(string fileName, string content)
        {
            var path = Path.Combine(OutputPath, fileName);
            File.WriteAllText(path, content, Encoding.UTF8);
        }

        private sealed class RpcServiceInfo
        {
            public Type Interface { get; }
            public string InterfaceName => Interface.Name;
            public int ServiceId { get; }
            public List<RpcMethodInfo> Methods { get; }

            public RpcServiceInfo(Type iface, int serviceId, List<RpcMethodInfo> methods)
            {
                Interface = iface;
                ServiceId = serviceId;
                Methods = methods;
            }
        }

        private sealed class RpcMethodInfo
        {
            public string Name { get; }
            public int MethodId { get; }
            public Type? ArgType { get; }
            public Type? RetType { get; }
            public bool IsVoid { get; }

            public RpcMethodInfo(string name, int methodId, Type? argType, Type? retType, bool isVoid)
            {
                Name = name;
                MethodId = methodId;
                ArgType = argType;
                RetType = retType;
                IsVoid = isVoid;
            }
        }
    }
}
