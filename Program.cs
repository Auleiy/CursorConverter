Console.Write("转换CUR还是ANI？[C/a]");
ConsoleKey k = Console.ReadKey().Key;
Console.WriteLine();
if (k == ConsoleKey.A)
    Ani.Convert();
else
    Cur.Convert();