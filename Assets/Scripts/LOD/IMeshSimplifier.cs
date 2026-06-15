namespace LOD
{
    /// <summary>
    /// Interface commune aux 3 méthodes de simplification.
    /// Convention : Simplify NE DOIT PAS muter "input". Cloner d'abord
    /// (input.Clone()) si la méthode édite le mesh en place (collapse, QEM).
    /// Le vertex clustering construit un mesh neuf, donc rien à cloner.
    ///
    /// targetTriangleCount = nombre de triangles visé en sortie. La méthode
    /// s'arrête dès qu'elle l'atteint (ou s'en approche au mieux pour le clustering,
    /// dont le compte est piloté indirectement par la taille de grille).
    /// </summary>
    public interface IMeshSimplifier
    {
        /// <summary>Nom affiché dans le harnais de comparaison / l'UI.</summary>
        string Name { get; }

        MyMesh Simplify(MyMesh input, int targetTriangleCount);
    }
}
