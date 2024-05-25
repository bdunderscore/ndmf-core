# ProxyNode (SerializedObject)

Key:

- IRenderFilter
- Group Inputs (instanceID and ProxyNode id set)

Value:

- Task<MeshState>
- IsInvalidated
- id (long, > int.MaxValue)

On recalculate:

- Build set of render targets
- Construct ProxyPipeline:
    - For each render target, compute IRenderFilters needed (in order) and their associated render groups
    - For each render filter, find the ProxyNode for that filter
    - Trigger leaf task
        - Await parents
        - If invalidated, abort
        - Invoke filter
    - Pipeline goes active when the final leaf node completes
        - Remove prior pipeline
        - Create new pipeline if needed
        - Perform GC (below)

Render:

- Traverse all pipeline steps, fire callbacks

GC:

- Mark all nodes referenced by pipelines
- Delete the rest

ProxyManager -> ProxySession -> ProxyPipeline, ProxyObjectController
ProxyManager is a global static, looks up ProxySession for the active camera
ProxySession manages pipeline creation/destruction, ProxyObjectController creation/destruction
ProxyObjectController manages a single object