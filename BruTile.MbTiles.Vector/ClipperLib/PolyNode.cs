#nullable disable

using Mapbox.Vector.Tile;

namespace BruTile.MbTiles.Vector.ClipperLib;

public class PolyNode
{
    internal PolyNode m_Parent;

    internal List<Coordinate> m_polygon = [];

    internal int m_Index;

    internal List<PolyNode> m_Childs = [];

    public int ChildCount => this.m_Childs.Count;

    public List<PolyNode> Childs
    {
        get
        {
            return this.m_Childs;
        }
    }

    public List<Coordinate> Contour => this.m_polygon;

    public bool IsHole => this.IsHoleNode();

    public PolyNode Parent => this.m_Parent;

    internal void AddChild(PolyNode Child)
    {
        var count = this.m_Childs.Count;
        this.m_Childs.Add(Child);
        Child.m_Parent = this;
        Child.m_Index = count;
    }

    public PolyNode GetNext()
    {
        if (this.m_Childs.Count <= 0)
        {
            return this.GetNextSiblingUp();
        }
        return this.m_Childs[0];
    }

    internal PolyNode GetNextSiblingUp()
    {
        if (this.m_Parent == null)
        {
            return null;
        }
        if (this.m_Index == this.m_Parent.m_Childs.Count - 1)
        {
            return this.m_Parent.GetNextSiblingUp();
        }
        return this.m_Parent.m_Childs[this.m_Index + 1];
    }

    private bool IsHoleNode()
    {
        var flag = true;
        for (var i = this.m_Parent; i != null; i = i.m_Parent)
        {
            flag = !flag;
        }
        return flag;
    }
}