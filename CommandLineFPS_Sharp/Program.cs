using System;
using System.Collections.Generic;
using System.Diagnostics;

class Program
{
    const int nScreenWidth = 120;         // Console Screen Size X (columns)
    const int nScreenHeight = 40;         // Console Screen Size Y (rows)
    const int nMapWidth = 16;             // World Dimensions
    const int nMapHeight = 16;

    static float fPlayerX = 14.7f;         // Player Start Position
    static float fPlayerY = 5.09f;
    static float fPlayerA = 0.0f;          // Player Start Rotation
    const float fFOV = 3.14159f / 4.0f;   // Field of View
    const float fDepth = 16.0f;           // Maximum rendering distance
    const float fSpeed = 5.0f;            // Walking Speed

    static void Main(string[] args)
    {
        // Create Screen Buffer
        char[] screen = new char[nScreenWidth * nScreenHeight];
        //                                               GENERIC_READ | GENERIC_WRITE        CONSOLE_TEXTMODE_BUFFER
        IntPtr hConsole = Native.CreateConsoleScreenBuffer(0x80000000 | 0x40000000, 0, IntPtr.Zero, 0x00000001, IntPtr.Zero);
        Native.SetConsoleActiveScreenBuffer(hConsole);
        uint dwBytesWritten = 0;

        string map = null;
        map += "#########.......";
        map += "#...............";
        map += "#.......########";
        map += "#..............#";
        map += "#......##......#";
        map += "#......##......#";
        map += "#..............#";
        map += "###............#";
        map += "##.............#";
        map += "#......####..###";
        map += "#......#.......#";
        map += "#......#.......#";
        map += "#..............#";
        map += "#......#########";
        map += "#..............#";
        map += "################";

        //GetTimestamp should be precise enough to be analagous to 
        //chrono::system_clock::now() . We also use the TimeSpan type, which 
        //is analagous to a time_point
        var tp1 = new TimeSpan(Stopwatch.GetTimestamp());
        var tp2 = new TimeSpan(Stopwatch.GetTimestamp());

        while (true)
        {
            // We'll need time differential per frame to calculate modification
            // to movement speeds, to ensure consistant movement, as ray-tracing
            // is non-deterministic
            tp2 = new TimeSpan(Stopwatch.GetTimestamp());
            float fElapsedTime = (float)(tp2 - tp1).TotalSeconds; //equivalent to count()
            tp1 = tp2;


            // Handle CCW Rotation
            if (Native.ManagedGetKeyState(Keys.A))
                fPlayerA -= (fSpeed * 0.75f) * fElapsedTime;

		    // Handle CW Rotation
		    if (Native.ManagedGetKeyState(Keys.D))
                fPlayerA += (fSpeed * 0.75f) * fElapsedTime;
		
		    // Handle Forwards movement & collision
		    if (Native.ManagedGetKeyState(Keys.W))
		    {
			    fPlayerX += MathF.Sin(fPlayerA) * fSpeed * fElapsedTime;
			    fPlayerY += MathF.Cos(fPlayerA) * fSpeed * fElapsedTime;
			    if (map[(int)fPlayerX * nMapWidth + (int)fPlayerY] == '#')
			    {
				    fPlayerX -= MathF.Sin(fPlayerA) * fSpeed * fElapsedTime;
				    fPlayerY -= MathF.Cos(fPlayerA) * fSpeed * fElapsedTime;
			    }			
		    }

            // Handle backwards movement & collision
            if (Native.ManagedGetKeyState(Keys.S))
		    {
                fPlayerX -= MathF.Sin(fPlayerA) * fSpeed * fElapsedTime;
                fPlayerY -= MathF.Cos(fPlayerA) * fSpeed * fElapsedTime;
                if (map[(int)fPlayerX * nMapWidth + (int)fPlayerY] == '#')
                {
                    fPlayerX += MathF.Sin(fPlayerA) * fSpeed * fElapsedTime;
                    fPlayerY += MathF.Cos(fPlayerA) * fSpeed * fElapsedTime;
                }
            }

            for (int x = 0; x < nScreenWidth; x++)
            {
                // For each column, calculate the projected ray angle into world space

                //                                           At least one of these has to be cast to float to work properly
                float fRayAngle = (fPlayerA - fFOV / 2.0f) + ((float)x / (float)nScreenWidth) * fFOV;

                // Find distance to wall
                float fStepSize = 0.1f;       // Increment size for ray casting, decrease to increase										
                float fDistanceToWall = 0.0f; //                                      resolution

                bool bHitWall = false;      // Set when ray hits wall block
                bool bBoundary = false;     // Set when ray hits boundary between two wall blocks

                float fEyeX = MathF.Sin(fRayAngle); // Unit vector for ray in player space
                float fEyeY = MathF.Cos(fRayAngle);

                // Incrementally cast ray from player, along ray angle, testing for 
                // intersection with a block
                while (!bHitWall && fDistanceToWall < fDepth)
                {
                    fDistanceToWall += fStepSize;
                    int nTestX = (int)(fPlayerX + fEyeX * fDistanceToWall);
                    int nTestY = (int)(fPlayerY + fEyeY * fDistanceToWall);

                    // Test if ray is out of bounds
                    if (nTestX < 0 || nTestX >= nMapWidth || nTestY < 0 || nTestY >= nMapHeight)
                    {
                        bHitWall = true;            // Just set distance to maximum depth
                        fDistanceToWall = fDepth;
                    }
                    else
                    {
                        // Ray is inbounds so test to see if the ray cell is a wall block
                        if (map[nTestX * nMapWidth + nTestY] == '#')
                        {
                            // Ray has hit wall
                            bHitWall = true;

                            // To highlight tile boundaries, cast a ray from each corner
                            // of the tile, to the player. The more coincident this ray
                            // is to the rendering ray, the closer we are to a tile 
                            // boundary, which we'll shade to add detail to the walls

                            //Our equivalent of a vector in C# is List, and our equivalent of a pair is a ValueTuple
                            List<(float first, float second)> p = new List<(float, float)>();

                            // Test each corner of hit tile, storing the distance from
                            // the player, and the calculated dot product of the two rays
                            for (int tx = 0; tx < 2; tx++)
                                for (int ty = 0; ty < 2; ty++)
                                {
                                    // Angle of corner to eye
                                    float vy = (float)nTestY + ty - fPlayerY;
                                    float vx = (float)nTestX + tx - fPlayerX;
                                    float d = MathF.Sqrt(vx * vx + vy * vy);
                                    float dot = (fEyeX * vx / d) + (fEyeY * vy / d);
                                    p.Add((d, dot));
                                }

                            // Sort Pairs from closest to farthest

                            //We use the default float comparators, as List<T>'s sort implementation expects
                            // -1, 0, or 1 depending on equality
                            p.Sort((left, right) => right.first.CompareTo(left.first));

                            // First two/three are closest (we will never see all four)
                            float fBound = 0.01f;
                            if (MathF.Acos(p[0].second) < fBound) bBoundary = true;
                            if (MathF.Acos(p[1].second) < fBound) bBoundary = true;
                            if (MathF.Acos(p[2].second) < fBound) bBoundary = true;
                        }
                    }
                }

                // Calculate distance to ceiling and floor
                int nCeiling = (int)( (float)(nScreenHeight / 2.0) - nScreenHeight / (float)fDistanceToWall );
                int nFloor = nScreenHeight - nCeiling;

                // Shader walls based on distance
                char nShade = ' ';
                if (fDistanceToWall <= fDepth / 4.0f)            nShade = (char)0x2588;    // Very close	
                else if (fDistanceToWall < fDepth / 3.0f)        nShade = (char)0x2593;
                else if (fDistanceToWall < fDepth / 2.0f)        nShade = (char)0x2592;
                else if (fDistanceToWall < fDepth)               nShade = (char)0x2591;
                else                                             nShade = ' ';		       // Too far away

                if (bBoundary)    nShade = ' '; // Black it out

                for (int y = 0; y < nScreenHeight; y++)
                {
                    // Each Row
                    if (y <= nCeiling)
                        screen[y * nScreenWidth + x] = ' ';
                    else if (y > nCeiling && y <= nFloor)
                        screen[y * nScreenWidth + x] = nShade;
                    else // Floor
                    {
                        // Shade floor based on distance
                        float b = 1.0f - ((y - nScreenHeight / 2.0f) / (nScreenHeight / 2.0f));
                        if (b < 0.25)       nShade = '#';
                        else if (b < 0.5)   nShade = 'x';
                        else if (b < 0.75)  nShade = '.';
                        else if (b < 0.9)   nShade = '-';
                        else                nShade = ' ';
                        screen[y * nScreenWidth + x] = nShade;
                    }
                }
            }

            // Display Stats

            // In C#, we can't exactly sprintf, so we format the string normally then copy it to the buffer
            string stats = string.Format("X={0}, Y={1}, A={2} FPS={3}", fPlayerX, fPlayerY, fPlayerA, 1.0f / fElapsedTime);
            stats.CopyTo(0, screen, 0, stats.Length);

            // Display Map
            for (int nx = 0; nx < nMapWidth; nx++)
                for (int ny = 0; ny < nMapWidth; ny++)
                {
                    screen[(ny + 1) * nScreenWidth + nx] = map[ny * nMapWidth + nx];
                }
            screen[((int)fPlayerX + 1) * nScreenWidth + (int)fPlayerY] = 'P';

            // Display Frame
            screen[nScreenWidth * nScreenHeight - 1] = '\0';

            //One last p/invoke call to write to our console buffer - this offers better performance than stdout or 
            //a combination of SetCursorPosition and Write
            Native.WriteConsoleOutputCharacter(hConsole, screen, nScreenWidth * nScreenHeight, new Native.COORD(), out dwBytesWritten);
        }
    }
}
