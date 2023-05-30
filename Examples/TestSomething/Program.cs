﻿using System.Linq.Expressions;
using TestSomething;

internal class Program
{
    private static async Task Main(string[] args)
    {

        //TestTreeCascadeAction();
        //await TestAspectInjectorAsync();
        //new TestRewriteMatch().Test();
        new MessagePackTest().Test();
    }

    private static async Task TestAspectInjectorAsync()
    {
        try
        {
            //new Node().MethodWithPushAspectApplied("Hello");
            await new Node().MethodWithPushAspectAppliedAsync("Hello from async");
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
        finally
        {
            Console.ReadLine();
        }
       
    }

    private static void TestTreeCascadeAction()
    {
        Console.WriteLine("Hello, World!");
        var node = new Node
        {
            Id = 1,
            Childs = new List<Node>
    {
        new Node {
            Id=2,
            Childs=new List<Node> {
                new Node {
                    Id=3, Childs=new List<Node>
                    {
                    new Node { Id=4,}
                }
                }
            }},
        new Node { Id=5,}
    }
        };

        node.CascadeAction(x =>
        {
            x.Id2 = x.Id * 10;
            Console.WriteLine($"Node:{x.Id},{x.Id2}");
        });
        foreach (var item in node.CascadeFunc(x => x))
        {
            Console.WriteLine($"Node Id:{item.Id}");
        }
    }
}