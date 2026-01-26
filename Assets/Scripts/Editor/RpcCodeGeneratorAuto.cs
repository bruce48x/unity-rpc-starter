using UnityEditor;
using UnityEngine;

namespace Game.Rpc.Editor
{
    public sealed class RpcCodeGeneratorAuto : AssetPostprocessor
    {
        private const string ContractsPackagePath = "Packages/com.bruce.rpc.contracts/";
        private const string ContractsAssetsPath = "Assets/Scripts/Rpc/Contracts/";
        private const string GeneratedPath = "Assets/Scripts/Rpc/GeneratedManual/";

        private static bool _pending;

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (RpcCodeGenerator.IsGenerating)
                return;

            if (!HasContractChanges(importedAssets) &&
                !HasContractChanges(deletedAssets) &&
                !HasContractChanges(movedAssets) &&
                !HasContractChanges(movedFromAssetPaths))
                return;

            if (_pending)
                return;

            _pending = true;
            EditorApplication.delayCall += TryGenerate;
        }

        private static bool HasContractChanges(string[] paths)
        {
            foreach (var path in paths)
            {
                if (path.StartsWith(GeneratedPath))
                    continue;
                if (path.StartsWith(ContractsPackagePath) || path.StartsWith(ContractsAssetsPath))
                    return true;
            }
            return false;
        }

        private static void TryGenerate()
        {
            if (EditorApplication.isCompiling)
            {
                EditorApplication.delayCall += TryGenerate;
                return;
            }

            _pending = false;
            RpcCodeGenerator.GenerateAuto();
            Debug.Log("[RpcCodeGenerator] Auto-generated RPC stubs (contracts changed).");
        }
    }
}
