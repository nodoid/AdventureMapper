using Xunit;

// Several services (autosave preferences, the pending-file queue, the UI culture) hold
// process-wide state, so the suite runs one test at a time.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
