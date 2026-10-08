// xunit.v3 moved parallelization control out of the Xunit namespace entirely: both
// CollectionBehaviorAttribute.DisableTestParallelization and .MaxParallelThreads are now
// obsolete (CS0619, fatal under TreatWarningsAsErrors). The replacement is
// Xunit.v3.ParallelizationAttribute.MaxThreads - note the Xunit.v3 namespace, verified
// against the xunit.v3 4.0.1 assembly metadata. MaxThreads = 0 preserves the original
// suite's serial execution exactly.
using Xunit.v3;

[assembly: Parallelization(MaxThreads = 0)]
