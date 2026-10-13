using UnityEngine;

/// <summary>
/// Convierte la posición del mouse en un punto del plano XY del juego.
/// Funciona con cámara ortográfica y de perspectiva.
/// </summary>
public static class MouseWorld2D
{
    public static bool TryGetPointOnPlane(
        Camera camera,
        Vector3 screenPosition,
        float planeZ,
        out Vector2 worldPoint)
    {
        worldPoint = Vector2.zero;

        if (camera == null)
            return false;

        Ray ray = camera.ScreenPointToRay(screenPosition);

        // El juego 2D está en el plano XY; Z fija el plano de juego.
        Plane gameplayPlane = new Plane(
            Vector3.forward,
            new Vector3(0f, 0f, planeZ)
        );

        if (!gameplayPlane.Raycast(ray, out float distance) || distance < 0f)
            return false;

        Vector3 point = ray.GetPoint(distance);
        worldPoint = new Vector2(point.x, point.y);
        return true;
    }
}
