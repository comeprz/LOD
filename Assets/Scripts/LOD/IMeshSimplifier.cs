namespace LOD
{
    // Interface commune aux 3 méthodes LOD
    public interface IMeshSimplifier
    {
        string Name { get; }

        MyMesh Simplify(MyMesh input, int targetTriangleCount);
    }
}
