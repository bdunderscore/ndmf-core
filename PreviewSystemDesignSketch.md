Use cases:

* Modify existing mesh
* Clone mesh for further modifications (e.g. FaceEmo previews)

# Sequence Points

We want 1/ to have consistent processing order with NDMF proper and 2/ to not depend on NDMF directly. So, we add a
layer of indirection with a SequencePoint:

```
ReactiveQuery<Mesh, MeshMutator> query = ...
SequencePoint mySequencePoint = new SequencePoint(); 

InPhase(...)
  .Run(...)
  .ThenSequence(mySequencePoint);

MeshPreview.RegisterMeshPreview(mySequencePoint, query);

Or, for convenience:

InPhase(...)
  .Run(...)
  .ThenMeshPreview(query);


```

# Mesh manipulation

```
ReactiveQuery<Mesh, MeshMutator> query = ReactiveQuery.create("my mesh mutator", (ctx, mesh) => {
    return MeshMutator.OnInit(mesh => {
        mesh.bones = ...;
    });
    
    // or
    
    return MeshMutator.OnFrame(mesh => {
        mesh.blendShapes = ...;
    });
    
    // these can be combined:
    
    return MeshMutator.OnInit(mesh => {
        mesh.bones = ...;
    }).OnFrame(mesh => {
        mesh.blendShapes = ...;
    });
});
```

TODO: Rename QueryCache -> ReactiveQuery; rename ReactiveQuery -> ReactiveValue?
