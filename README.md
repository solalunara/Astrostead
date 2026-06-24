# Astrostead: Cylindrical and Spherical Voxels in Unity

## The Goal

The idea behind voxels is that the world is discretized into small voluminous sections, analogous to how a screen is discretized into pixels, hence the name "voxels." This concept allows any 3D object or structure to be deformed in any way possible up to the resolution of the voxels, and is extremely powerful as a means of letting a player interact with a simulated world. Probably the most well known implementation of this concept exists in the game Minecraft, which is entirely based on the world being constructed of voxels and allows an extremely broad level of player interactivity.

One limitation of the world being constructed of these voxels, however, is that it enforces a cartesian structure on the world, through the nature of the grid in which the voxels take their shape. This means that, for instance, it would be incredibly impractical to have spherical objects in a game like Minecraft, as anywhere other than the x/y/z extremities the structure of the voxel grid would appear to be at an angle relative to an observer who's vertical up direction is radially outward from the sphere, as would be the case for an observer on a planet.

The goal of this project, therefore, is to use the voxel-based approach for cylindrical and spherical grids to make structures that can reflect non-cartesian geometries.

---

## Design Challenges

### Exposed Neighbor Occlusion

Performance is a critical factor in voxel-based systems. Using a naïve approach of each voxel being its own object with no structure optimizations, performance rapidly breaks down at relatively small numbers of voxels. One of the biggest performance optimizations is only rendering the faces of voxels which are exposed, and hiding the rest. In cartesian geometries this is relatively simple, as one only needs to check each of the six cardinal directions and update voxels when their neighbors change. In non-cartesian geometries the problem becomes much more complicated.

One of the main principles of voxels in cartesian geometries is that each voxel is the same size. While we want to make our voxel sizes as close as possible in non-cartesian geometries, it would be impossible to make them all exactly the same size. Other approaches to spherical geometries have used a mix of hexagons and pentagons, but even so the size of the voxels has to change as radial distance from the centre of the geometry changes.

To preserve the same approach of only rendering exposed faces and re-checking when the neighbors of a voxel are updated, we need generalized methods of getting the neighbors of a given voxel that can return between 1 and an arbitrarily high number of neighbor grid positions. We then can loop through these neighbors and check if any of them don't contain a voxel, and in such case choose to render the face of the voxel pointing toward said neighbor grid position.

Other performance optimizations are relatively straightforward to implement as they don't require special consideration for non-cartesian geometries. The optimizations included in this project included combining many voxels into a single mesh when not being interacted with and all voxels using a single texture, known as the texture atlas, with different textures being represented using specific UV coordinates.

### Voxel Continuity

To attempt to make the voxels approximately the same size despite the variability in lengths due to the geometry, we want to allow certain properties of the geometry to change with position. For example, in cylindrical geometry, we want δθ, the θ width of each voxel, to change with radius such that it is at a minimum π/4 at r = 0 and decreases to maintain rδθ ≈ const.

Doing this, however, introduces a discontinuity in the voxels at different radii, as the radius of the voxel approximation of the cylinder falls short of the true value between the endpoints. To correct for this, we will need some way of adjusting the vertices inward of voxels on the outer side of the boundary. This effect is visualized in the diagram below, in which the correction factor necessary was also calculated, however using Unity's LERP feature was found to not be a significant factor on performance.

In the case of spherical geometry the situation is even more complicated, as both δϕ and δθ can vary, with δϕ itself varying with θ. This is an in-progress part of this project.

---

## Implementation

Unity helpfully provides access to the OpenGL primitives used during rendering, including vertex data, UV data, and triangle indices. Using the Unity primitive of the cube as a base, we can modify the vertex data to match specific grid positions for a generic grid, specified as cartesian, cylindrical, or spherical, subtracting the vertex that acts as the origin of the voxel.

As discussed, each grid must implement some function to get the neighbor grid positions, and which face corresponds to each one. This functionality is outlined in an abstract "Voxel Group" class, which also outlines a method to convert voxel indexes to local, physical coordinates, and how to translate between indices. The latter of those is not a completely trivial matter. Taking spherical coordinates as an example, when increasing or decreasing the r index one must take care to keep the θ and ϕ indices consistent with the increased or decreased δθ and δϕ.

An example cylindrical voxel group is shown below, where a cylinder was initially constructed before being partially hollowed out. Though voxel continuity for spherical geometries is still in progress, an example spherical voxel group is also depicted.

---

## Source and Future Work

The source for the project, named Astrostead, can be found at **https://github.com/solalunara/Astrostead**. While AI provides many benefits, it was not used in this project or in the writing of this readme/report as the aim of this project is primarily learning rather than producing a developed product at speed. Similarly, this readme/report was interesting to me on a personal level. AI has been used, however, to craft the commit message of commits done through github directly, such as updating this readme.

As mentioned previously, voxel continuity in the spherical case is an active goal of the project. Another planned extension is connecting voxel groups of the same geometry and properties, such that a very large spherical/cylindrical structure could be made of many large chunks of voxels collected together in a voxel group.


![Diagram of the problem introduced by allowing $\delta\theta$ to vary with radius in cylindrical geometry, and a mathematical description of the adjustment d necessary to the inner vertices of the voxel on the outer boundary](ReadmeImages/cylinderdrawing.png)

![Partially hollowed-out cylinder constructed using a cylindrical voxel geometry, incorporating the voxel continuity effect. The cylinder represents a primitive player model, standing on the cylinder](ReadmeImages/cylinder.png)


![Partially cut sphere constructed using a spherical voxel geometry. The cylinder again represents a primitive player model, standing on the cylinder](ReadmeImages/sphere.png)
