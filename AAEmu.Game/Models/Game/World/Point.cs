﻿using System;
using System.Collections.Generic;

namespace AAEmu.Game.Models.Game.World;

public class Point
{
    public uint WorldId { get; set; }
    public uint ZoneId { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public sbyte RotationX { get; set; }
    public sbyte RotationY { get; set; }
    public sbyte RotationZ { get; set; }

    public Point()
    {
    }

    public Point(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public Point(float x, float y, float z, sbyte rotationX, sbyte rotationY, sbyte rotationZ)
    {
        X = x;
        Y = y;
        Z = z;
        RotationX = rotationX;
        RotationY = rotationY;
        RotationZ = rotationZ;
    }

    public Point(uint worldId, uint zoneId, float x, float y, float z, sbyte rotationX, sbyte rotationY, sbyte rotationZ)
    {
        WorldId = worldId;
        ZoneId = zoneId;
        X = x;
        Y = y;
        Z = z;
        RotationX = rotationX;
        RotationY = rotationY;
        RotationZ = rotationZ;
    }

    public Point Clone()
    {
        return new Point(WorldId, ZoneId, X, Y, Z, RotationX, RotationY, RotationZ);
    }

    internal static bool OnSegment(Point p, Point q, Point r)
    {
        if (q.X <= Math.Max(p.X, r.X) &&
            q.X >= Math.Min(p.X, r.X) &&
            q.Y <= Math.Max(p.Y, r.Y) &&
            q.Y >= Math.Min(p.Y, r.Y))
        {
            return true;
        }
        return false;
    }

    // To find orientation of ordered triplet (p, q, r).
    // The function returns following values
    // 0 --> p, q and r are colinear
    // 1 --> Clockwise
    // 2 --> Counterclockwise
    internal static int FindTripletOrientation(Point p, Point q, Point r)
    {
        float val = (q.Y - p.Y) * (r.X - q.X) -
                (q.X - p.X) * (r.Y - q.Y);

        if (val == 0.0f)
        {
            return 0; // colinear
        }
        return (val > 0) ? 1 : 2; // clock or counterclock wise
    }

    // The function that returns true if
    // line segment 'p1q1' and 'p2q2' intersect.
    internal static bool IsLineIntersection(
        (Point p, Point q) line1,
        (Point p, Point q) line2)
    {
        // Find the four orientations needed for
        // general and special cases
        int o1 = FindTripletOrientation(line1.p, line1.q, line2.p);
        int o2 = FindTripletOrientation(line1.p, line1.q, line2.q);
        int o3 = FindTripletOrientation(line2.p, line2.q, line1.p);
        int o4 = FindTripletOrientation(line2.p, line2.q, line1.q);

        // General case
        if (o1 != o2 && o3 != o4)
        {
            return true;
        }

        // Special Cases
        // p1, q1 and p2 are colinear and
        // p2 lies on segment p1q1
        if (o1 == 0 && OnSegment(line1.p, line2.p, line1.q))
        {
            return true;
        }

        // p1, q1 and p2 are colinear and
        // q2 lies on segment p1q1
        if (o2 == 0 && OnSegment(line1.p, line2.q, line1.q))
        {
            return true;
        }

        // p2, q2 and p1 are colinear and
        // p1 lies on segment p2q2
        if (o3 == 0 && OnSegment(line2.p, line1.p, line2.q))
        {
            return true;
        }

        // p2, q2 and q1 are colinear and
        // q1 lies on segment p2q2
        if (o4 == 0 && OnSegment(line2.p, line1.q, line2.q))
        {
            return true;
        }

        // Doesn't fall in any of the above cases
        return false;
    }

    // Returns true if the point p lies
    // inside the polygon[] with n vertices
    public static bool IsInside(IReadOnlyList<Point> polygon, int n, Point p)
    {
        // There must be at least 3 vertices in polygon[]
        if (n < 3)
        {
            return false;
        }

        // Create a point for line segment from p to infinite
        var extreme = new Point(1000, p.Y, 0);

        // Count intersections of the above line
        // with sides of polygon
        int count = 0, i = 0;
        do
        {
            int next = (i + 1) % n;

            // Check if the line segment from 'p' to
            // 'extreme' intersects with the line
            // segment from 'polygon[i]' to 'polygon[next]'
            if (IsLineIntersection(
                line1: (polygon[i], polygon[next]),
                line2: (p, extreme)))
            {
                // If the point 'p' is colinear with line
                // segment 'i-next', then check if it lies
                // on segment. If it lies, return true, otherwise false
                if (FindTripletOrientation(polygon[i], p, polygon[next]) == 0)
                {
                    return OnSegment(polygon[i], p,
                                    polygon[next]);
                }
                count++;
            }
            i = next;
        } while (i != 0);

        // Return true if count is odd, false otherwise
        return (count % 2 == 1); // Same as (count%2 == 1)
    }
}
