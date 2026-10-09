namespace ScamDetector.Core.Text;

public static class EditDistance
{
    public static bool IsWithin(string first, string second, int maximumDistance) =>
        Math.Abs(first.Length - second.Length) <= maximumDistance
        && OptimalStringAlignment(first, second) <= maximumDistance;

    public static int OptimalStringAlignment(string first, string second)
    {
        var distances = new int[first.Length + 1, second.Length + 1];
        for (var row = 0; row <= first.Length; row++)
            distances[row, 0] = row;
        for (var column = 0; column <= second.Length; column++)
            distances[0, column] = column;

        for (var row = 1; row <= first.Length; row++)
        {
            for (var column = 1; column <= second.Length; column++)
                distances[row, column] = CellDistance(first, second, distances, row, column);
        }
        return distances[first.Length, second.Length];
    }

    private static int CellDistance(string first, string second, int[,] distances, int row, int column)
    {
        var substitutionCost = first[row - 1] == second[column - 1] ? 0 : 1;
        var distance = Math.Min(
            Math.Min(distances[row - 1, column] + 1, distances[row, column - 1] + 1),
            distances[row - 1, column - 1] + substitutionCost);
        var isTransposition = row > 1 && column > 1
            && first[row - 1] == second[column - 2] && first[row - 2] == second[column - 1];
        return isTransposition ? Math.Min(distance, distances[row - 2, column - 2] + 1) : distance;
    }
}
