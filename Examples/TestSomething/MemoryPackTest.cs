﻿using MemoryPack;
using System;

public class MyObject
{
    public string Name { get; set; }
    public int Age { get; set; }
}

public class MemoryPackTest
{
    public void Run()
    {
        // Create an object to serialize.
        MyObject myObject = new MyObject
        {
            Name = "John Doe",
            Age = 30
        };
        var bin = MemoryPackSerializer.Serialize<dynamic>(myObject);
        var val = MemoryPackSerializer.Deserialize<MyObject>(bin);
    }
}