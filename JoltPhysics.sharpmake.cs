using System;
using System.IO;

using Sharpmake;

namespace VoltSharpmake
{
    // Builds ONLY the core Jolt physics static library (the `Jolt/` subfolder).
    // Samples, TestFramework, JoltViewer, PerformanceTest, HelloWorld and UnitTests
    // are intentionally excluded - we point SourceRootPath at the Jolt lib dir only.
    //
    // The GPU compute backends (Compute/DX12, Compute/VK, Compute/MTL, Compute/CPU)
    // and the HLSL shaders live inside Jolt/ and get globbed too, but:
    //   - every compute .cpp self-guards with #ifdef JPH_USE_DX12 / _VK / _MTL / _CPU_COMPUTE,
    //     so without those defines they compile to empty translation units.
    //   - .hlsl / .mm / .metal are not in Sharpmake's default source extensions, so they
    //     are never added to the project (no fxc/Metal build steps to replicate).
    // Result: a pure-CPU Jolt lib. Enable a JPH_USE_* define below if you want a backend.
    [Sharpmake.Generate]
    public class JoltPhysics : CommonThirdPartyLibProject
    {
        public JoltPhysics() : base()
        {
            Name = "JoltPhysics";

            // Compile only the core library, not the sample/test/viewer projects.
            SourceRootPath = @"[project.RootPath]/JoltPhysics/Jolt";
        }

        public override void ConfigureAll(Configuration conf, CommonTarget target)
        {
            base.ConfigureAll(conf, target);

            // Jolt headers are included as <Jolt/...>, so the repo root must be on the
            // include path (exported to dependents so they can #include <Jolt/Jolt.h>).
            conf.IncludePaths.Add(@"[project.RootPath]/JoltPhysics");

            // ---- ABI-critical PUBLIC defines --------------------------------------
            // These change struct layout / vector width. They MUST match between the
            // library and every consumer, otherwise you get silent corruption or link
            // errors. Mirror Jolt's CMake defaults. Add to both Defines (this lib) and
            // ExportDefines (anything that depends on JoltPhysics).
            string[] publicDefines =
            {
                // x86 instruction sets (CMake defaults: everything but AVX512 on).
                "JPH_USE_AVX2",
                "JPH_USE_AVX",
                "JPH_USE_SSE4_1",
                "JPH_USE_SSE4_2",
                "JPH_USE_LZCNT",
                "JPH_USE_TZCNT",
                "JPH_USE_F16C",
                "JPH_USE_FMADD",

                // ObjectLayer width (CMake default 16).
                "JPH_OBJECT_LAYER_BITS=16",

                // ObjectStream + RTTI attribute info (ENABLE_OBJECT_STREAM default ON).
                "JPH_OBJECT_STREAM",
            };

            foreach (string define in publicDefines)
            {
                conf.Defines.Add(define);
                conf.ExportDefines.Add(define);
            }

            // Debug renderer + profiler: on in Debug/Development, off in Distribution
            // (matches Jolt's DEBUG_RENDERER_IN_DEBUG_AND_RELEASE / PROFILER_IN_DEBUG_AND_RELEASE).
            if (target.Optimization != Optimization.Dist)
            {
                conf.Defines.Add("JPH_DEBUG_RENDERER");
                conf.ExportDefines.Add("JPH_DEBUG_RENDERER");
                conf.Defines.Add("JPH_PROFILE_ENABLED");
                conf.ExportDefines.Add("JPH_PROFILE_ENABLED");
            }

            // Enable Jolt asserts in debug builds (CMake USE_ASSERTS is off by default,
            // but asserts are the whole point of a debug physics build).
            if (target.Optimization == Optimization.Debug || target.Optimization == Optimization.Debug_ASAN)
            {
                conf.Defines.Add("JPH_ENABLE_ASSERTS");
                conf.ExportDefines.Add("JPH_ENABLE_ASSERTS");
            }

            // AVX2 codegen to back the JPH_USE_AVX2 define (CMake: /arch:AVX2).
            conf.Options.Add(Options.Vc.Compiler.EnhancedInstructionSet.AdvancedVectorExtensions2);
        }
    }
}
