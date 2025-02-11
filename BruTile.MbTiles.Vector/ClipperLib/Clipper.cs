#nullable disable

using Mapbox.Vector.Tile;

namespace BruTile.MbTiles.Vector.ClipperLib;

public class Clipper : ClipperBase
{
    private List<OutRec> mPolyOuts;

    private ClipType mClipType;

    private Scanbeam m_Scanbeam;

    private TEdge m_ActiveEdges;

    private TEdge m_SortedEdges;

    private IntersectNode m_IntersectNodes;

    private bool m_ExecuteLocked;

    private PolyFillType m_ClipFillType;

    private PolyFillType m_SubjFillType;

    private List<JoinRec> mJoins;

    private List<HorzJoinRec> mHorizJoins;

    private bool mReverseOutput;

    private bool m_UsingPolyTree;

    public Clipper()
    {
        m_Scanbeam = null;
        m_ActiveEdges = null;
        m_SortedEdges = null;
        m_IntersectNodes = null;
        m_ExecuteLocked = false;
        m_UsingPolyTree = false;
        mPolyOuts = new List<OutRec>();
        mJoins = new List<JoinRec>();
        mHorizJoins = new List<HorzJoinRec>();
        mReverseOutput = false;
    }

    private void AddEdgeToSEL(TEdge edge)
    {
        if (m_SortedEdges == null)
        {
            m_SortedEdges = edge;
            edge.prevInSEL = null;
            edge.nextInSEL = null;
            return;
        }

        edge.nextInSEL = m_SortedEdges;
        edge.prevInSEL = null;
        m_SortedEdges.prevInSEL = edge;
        m_SortedEdges = edge;
    }

    private void AddHorzJoin(TEdge e, int idx)
    {
        var horzJoinRec = new HorzJoinRec()
        {
            edge = e,
            savedIdx = idx
        };
        mHorizJoins.Add(horzJoinRec);
    }

    private void AddIntersectNode(TEdge e1, TEdge e2, Coordinate pt)
    {
        var intersectNode = new IntersectNode()
        {
            edge1 = e1,
            edge2 = e2,
            pt = pt,
            next = null
        };
        if (m_IntersectNodes == null)
        {
            m_IntersectNodes = intersectNode;
            return;
        }

        if (ProcessParam1BeforeParam2(intersectNode, m_IntersectNodes))
        {
            intersectNode.next = m_IntersectNodes;
            m_IntersectNodes = intersectNode;
            return;
        }

        var mIntersectNodes = m_IntersectNodes;
        while (mIntersectNodes.next != null && ProcessParam1BeforeParam2(mIntersectNodes.next, intersectNode))
        {
            mIntersectNodes = mIntersectNodes.next;
        }

        intersectNode.next = mIntersectNodes.next;
        mIntersectNodes.next = intersectNode;
    }

    private void AddJoin(TEdge e1, TEdge e2, int e1OutIdx, int e2OutIdx)
    {
        var joinRec = new JoinRec();
        if (e1OutIdx < 0)
        {
            joinRec.poly1Idx = e1.outIdx;
        }
        else
        {
            joinRec.poly1Idx = e1OutIdx;
        }

        joinRec.pt1a = new Coordinate
        {
            X = e1.xcurr,
            Y = e1.ycurr
        };
        joinRec.pt1b = new Coordinate
        {
            X = e1.xtop,
            Y = e1.ytop
        };
        if (e2OutIdx < 0)
        {
            joinRec.poly2Idx = e2.outIdx;
        }
        else
        {
            joinRec.poly2Idx = e2OutIdx;
        }

        joinRec.pt2a = new Coordinate
        {
            X = e2.xcurr,
            Y = e2.ycurr
        };
        joinRec.pt2b = new Coordinate
        {
            X = e2.xtop,
            Y = e2.ytop
        };
        mJoins.Add(joinRec);
    }

    private void AddLocalMaxPoly(TEdge e1, TEdge e2, Coordinate pt)
    {
        AddOutPt(e1, pt);
        if (e1.outIdx == e2.outIdx)
        {
            e1.outIdx = -1;
            e2.outIdx = -1;
            return;
        }

        if (e1.outIdx < e2.outIdx)
        {
            AppendPolygon(e1, e2);
            return;
        }

        AppendPolygon(e2, e1);
    }

    private void AddLocalMinPoly(TEdge e1, TEdge e2, Coordinate pt)
    {
        TEdge tEdge;
        TEdge tEdge1;
        if (e2.dx == -3.4E+38 || e1.dx > e2.dx)
        {
            AddOutPt(e1, pt);
            e2.outIdx = e1.outIdx;
            e1.side = EdgeSide.esLeft;
            e2.side = EdgeSide.esRight;
            tEdge = e1;
            tEdge1 = (tEdge.prevInAEL != e2 ? tEdge.prevInAEL : e2.prevInAEL);
        }
        else
        {
            AddOutPt(e2, pt);
            e1.outIdx = e2.outIdx;
            e1.side = EdgeSide.esRight;
            e2.side = EdgeSide.esLeft;
            tEdge = e2;
            tEdge1 = (tEdge.prevInAEL != e1 ? tEdge.prevInAEL : e1.prevInAEL);
        }

        if (tEdge1 != null && tEdge1.outIdx >= 0 && TopX(tEdge1, pt.Y) == TopX(tEdge, pt.Y) && SlopesEqual(tEdge, tEdge1, m_UseFullRange))
        {
            AddJoin(tEdge, tEdge1, -1, -1);
        }
    }

    private void AddOutPt(TEdge e, Coordinate pt)
    {
        var flag = e.side == EdgeSide.esLeft;
        if (e.outIdx < 0)
        {
            var count = CreateOutRec();
            mPolyOuts.Add(count);
            count.idx = mPolyOuts.Count - 1;
            e.outIdx = count.idx;
            var outPt = new OutPt();
            count.pts = outPt;
            count.bottomPt = outPt;
            outPt.pt = pt;
            outPt.idx = count.idx;
            outPt.next = outPt;
            outPt.prev = outPt;
            SetHoleState(e, count);
            return;
        }

        var item = mPolyOuts[e.outIdx];
        var outPt1 = item.pts;
        if (flag && PointsEqual(pt, outPt1.pt) || !flag && PointsEqual(pt, outPt1.prev.pt))
        {
            return;
        }

        var outPt2 = new OutPt()
        {
            pt = pt,
            idx = item.idx
        };
        if (outPt2.pt.Y == item.bottomPt.pt.Y && outPt2.pt.X < item.bottomPt.pt.X)
        {
            item.bottomPt = outPt2;
        }

        outPt2.next = outPt1;
        outPt2.prev = outPt1.prev;
        outPt2.prev.next = outPt2;
        outPt1.prev = outPt2;
        if (flag)
        {
            item.pts = outPt2;
        }
    }

    public static void AddPolyNodeToPolygons(PolyNode polynode, List<List<Coordinate>> polygons)
    {
        if (polynode.Contour.Count > 0)
        {
            polygons.Add(polynode.Contour);
        }

        foreach (var child in polynode.Childs)
        {
            AddPolyNodeToPolygons(child, polygons);
        }
    }

    private void AppendPolygon(TEdge e1, TEdge e2)
    {
        OutRec outRec;
        EdgeSide edgeSide;
        var item = mPolyOuts[e1.outIdx];
        var item1 = mPolyOuts[e2.outIdx];
        if (!Param1RightOfParam2(item, item1))
        {
            outRec = (!Param1RightOfParam2(item1, item) ? GetLowermostRec(item, item1) : item);
        }
        else
        {
            outRec = item1;
        }

        var outPt = item.pts;
        var outPt1 = outPt.prev;
        var outPt2 = item1.pts;
        var outPt3 = outPt2.prev;
        if (e1.side != EdgeSide.esLeft)
        {
            if (e2.side != EdgeSide.esRight)
            {
                outPt1.next = outPt2;
                outPt2.prev = outPt1;
                outPt.prev = outPt3;
                outPt3.next = outPt;
            }
            else
            {
                ReversePolyPtLinks(outPt2);
                outPt1.next = outPt3;
                outPt3.prev = outPt1;
                outPt2.next = outPt;
                outPt.prev = outPt2;
            }

            edgeSide = EdgeSide.esRight;
        }
        else
        {
            if (e2.side != EdgeSide.esLeft)
            {
                outPt3.next = outPt;
                outPt.prev = outPt3;
                outPt2.prev = outPt1;
                outPt1.next = outPt2;
                item.pts = outPt2;
            }
            else
            {
                ReversePolyPtLinks(outPt2);
                outPt2.next = outPt;
                outPt.prev = outPt2;
                outPt1.next = outPt3;
                outPt3.prev = outPt1;
                item.pts = outPt3;
            }

            edgeSide = EdgeSide.esLeft;
        }

        if (outRec == item1)
        {
            item.bottomPt = item1.bottomPt;
            item.bottomPt.idx = item.idx;
            if (item1.FirstLeft != item)
            {
                item.FirstLeft = item1.FirstLeft;
            }

            item.isHole = item1.isHole;
        }

        item1.pts = null;
        item1.bottomPt = null;
        item1.FirstLeft = item;
        var num = e1.outIdx;
        var num1 = e2.outIdx;
        e1.outIdx = -1;
        e2.outIdx = -1;
        var mActiveEdges = m_ActiveEdges;
        while (mActiveEdges != null)
        {
            if (mActiveEdges.outIdx != num1)
            {
                mActiveEdges = mActiveEdges.nextInAEL;
            }
            else
            {
                mActiveEdges.outIdx = num;
                mActiveEdges.side = edgeSide;
                break;
            }
        }

        for (var i = 0; i < mJoins.Count; i++)
        {
            if (mJoins[i].poly1Idx == num1)
            {
                mJoins[i].poly1Idx = num;
            }

            if (mJoins[i].poly2Idx == num1)
            {
                mJoins[i].poly2Idx = num;
            }
        }

        for (var j = 0; j < mHorizJoins.Count; j++)
        {
            if (mHorizJoins[j].savedIdx == num1)
            {
                mHorizJoins[j].savedIdx = num;
            }
        }
    }

    public static double Area(List<Coordinate> poly)
    {
        var count = poly.Count - 1;
        if (count < 2)
        {
            return 0;
        }

        if (FullRangeNeeded(poly))
        {
            var int128 = new Int128(0);
            int128 = Int128.Int128Mul(poly[count].X + poly[0].X, poly[0].Y - poly[count].Y);
            for (var i = 1; i <= count; i++)
            {
                int128 += Int128.Int128Mul(poly[i - 1].X + poly[i].X, poly[i].Y - poly[i - 1].Y);
            }

            return int128.ToDouble() / 2;
        }

        var x = (poly[count].X + (double)poly[0].X) * (poly[0].Y - (double)poly[count].Y);
        for (var j = 1; j <= count; j++)
        {
            x = x + (poly[j - 1].X + (double)poly[j].X) * (poly[j].Y - (double)poly[j - 1].Y);
        }

        return x / 2;
    }

    private double Area(OutRec outRec, bool UseFull64BitRange)
    {
        var outPt = outRec.pts;
        if (outPt == null)
        {
            return 0;
        }

        if (!UseFull64BitRange)
        {
            double x = 0;
            do
            {
                x += (outPt.pt.X + outPt.prev.pt.X) * (outPt.prev.pt.Y - outPt.pt.Y);
                outPt = outPt.next;
            } while (outPt != outRec.pts);

            return x / 2;
        }

        var int128 = new Int128(0);
        do
        {
            int128 += Int128.Int128Mul(outPt.pt.X + outPt.prev.pt.X, outPt.prev.pt.Y - outPt.pt.Y);
            outPt = outPt.next;
        } while (outPt != outRec.pts);

        return int128.ToDouble() / 2;
    }

    internal static List<Coordinate> BuildArc(Coordinate pt, double a1, double a2, double r)
    {
        var num = (long)Math.Max(6, (int)(Math.Sqrt(Math.Abs(r)) * Math.Abs(a2 - a1)));
        if (num > 256)
        {
            num = 256;
        }

        var num1 = (int)num;
        var intPoints = new List<Coordinate>(num1);
        var num2 = (a2 - a1) / (num1 - 1);
        var num3 = a1;
        for (var i = 0; i < num1; i++)
        {
            intPoints.Add(
                new Coordinate
                {
                    X = pt.X + Round(Math.Cos(num3) * r),
                    Y = pt.Y + Round(Math.Sin(num3) * r)
                });
            num3 += num2;
        }

        return intPoints;
    }

    private void BuildIntersectList(long botY, long topY)
    {
        if (m_ActiveEdges == null)
        {
            return;
        }

        var mActiveEdges = m_ActiveEdges;
        m_SortedEdges = mActiveEdges;
        while (mActiveEdges != null)
        {
            mActiveEdges.prevInSEL = mActiveEdges.prevInAEL;
            mActiveEdges.nextInSEL = mActiveEdges.nextInAEL;
            mActiveEdges.tmpX = TopX(mActiveEdges, topY);
            mActiveEdges = mActiveEdges.nextInAEL;
        }

        var flag = true;
        while (flag && m_SortedEdges != null)
        {
            flag = false;
            mActiveEdges = m_SortedEdges;
            while (mActiveEdges.nextInSEL != null)
            {
                var tEdge = mActiveEdges.nextInSEL;
                var intPoint = new Coordinate();
                if (mActiveEdges.tmpX <= tEdge.tmpX || !IntersectPoint(mActiveEdges, tEdge, ref intPoint))
                {
                    mActiveEdges = tEdge;
                }
                else
                {
                    if (intPoint.Y > botY)
                    {
                        intPoint.Y = botY;
                        intPoint.X = TopX(mActiveEdges, intPoint.Y);
                    }

                    AddIntersectNode(mActiveEdges, tEdge, intPoint);
                    SwapPositionsInSEL(mActiveEdges, tEdge);
                    flag = true;
                }
            }

            if (mActiveEdges.prevInSEL == null)
            {
                break;
            }

            mActiveEdges.prevInSEL.nextInSEL = null;
        }

        m_SortedEdges = null;
    }

    private void BuildResult(List<List<Coordinate>> polyg)
    {
        polyg.Clear();
        polyg.Capacity = mPolyOuts.Count;
        for (var i = 0; i < mPolyOuts.Count; i++)
        {
            var item = mPolyOuts[i];
            if (item.pts != null)
            {
                var outPt = item.pts;
                var num = PointCount(outPt);
                if (num >= 3)
                {
                    var intPoints = new List<Coordinate>(num);
                    for (var j = 0; j < num; j++)
                    {
                        intPoints.Add(outPt.pt);
                        outPt = outPt.prev;
                    }

                    polyg.Add(intPoints);
                }
            }
        }
    }

    private void BuildResult2(PolyTree polytree)
    {
        polytree.Clear();
        polytree.m_AllPolys.Capacity = mPolyOuts.Count;
        for (var i = 0; i < mPolyOuts.Count; i++)
        {
            var item = mPolyOuts[i];
            var num = PointCount(item.pts);
            if (num >= 3)
            {
                FixHoleLinkage(item);
                var polyNode = new PolyNode();
                polytree.m_AllPolys.Add(polyNode);
                item.polyNode = polyNode;
                polyNode.m_polygon.Capacity = num;
                var outPt = item.pts;
                for (var j = 0; j < num; j++)
                {
                    polyNode.m_polygon.Add(outPt.pt);
                    outPt = outPt.prev;
                }
            }
        }

        polytree.m_Childs.Capacity = mPolyOuts.Count;
        for (var k = 0; k < mPolyOuts.Count; k++)
        {
            var count = mPolyOuts[k];
            if (count.polyNode != null)
            {
                if (count.FirstLeft != null)
                {
                    count.FirstLeft.polyNode.AddChild(count.polyNode);
                }
                else
                {
                    count.polyNode.m_Index = polytree.m_Childs.Count;
                    polytree.m_Childs.Add(count.polyNode);
                    count.polyNode.m_Parent = polytree;
                }
            }
        }
    }

    public static List<Coordinate> CleanPolygon(List<Coordinate> poly, double delta = 1.415)
    {
        var count = poly.Count;
        if (count < 3)
        {
            return null;
        }

        var intPoints = new List<Coordinate>(poly);
        var num = (int)(delta * delta);
        var item = poly[0];
        var num1 = 1;
        for (var i = 1; i < count; i++)
        {
            if ((poly[i].X - item.X) * (poly[i].X - item.X) + (poly[i].Y - item.Y) * (poly[i].Y - item.Y) > num)
            {
                intPoints[num1] = poly[i];
                item = poly[i];
                num1++;
            }
        }

        item = poly[num1 - 1];
        if ((poly[0].X - item.X) * (poly[0].X - item.X) + (poly[0].Y - item.Y) * (poly[0].Y - item.Y) <= num)
        {
            num1--;
        }

        if (num1 < count)
        {
            intPoints.RemoveRange(num1, count - num1);
        }

        return intPoints;
    }

    public override void Clear()
    {
        if (m_edges.Count == 0)
        {
            return;
        }

        DisposeAllPolyPts();
        base.Clear();
    }

    private void CopyAELToSEL()
    {
        var mActiveEdges = m_ActiveEdges;
        m_SortedEdges = mActiveEdges;
        if (m_ActiveEdges == null)
        {
            return;
        }

        m_SortedEdges.prevInSEL = null;
        for (mActiveEdges = mActiveEdges.nextInAEL; mActiveEdges != null; mActiveEdges = mActiveEdges.nextInAEL)
        {
            mActiveEdges.prevInSEL = mActiveEdges.prevInAEL;
            mActiveEdges.prevInSEL.nextInSEL = mActiveEdges;
            mActiveEdges.nextInSEL = null;
        }
    }

    private OutRec CreateOutRec()
    {
        var outRec = new OutRec()
        {
            idx = -1,
            isHole = false,
            FirstLeft = null,
            pts = null,
            bottomPt = null,
            polyNode = null
        };
        return outRec;
    }

    private void DeleteFromAEL(TEdge e)
    {
        var tEdge = e.prevInAEL;
        var tEdge1 = e.nextInAEL;
        if (tEdge == null && tEdge1 == null && e != m_ActiveEdges)
        {
            return;
        }

        if (tEdge == null)
        {
            m_ActiveEdges = tEdge1;
        }
        else
        {
            tEdge.nextInAEL = tEdge1;
        }

        if (tEdge1 != null)
        {
            tEdge1.prevInAEL = tEdge;
        }

        e.nextInAEL = null;
        e.prevInAEL = null;
    }

    private void DeleteFromSEL(TEdge e)
    {
        var tEdge = e.prevInSEL;
        var tEdge1 = e.nextInSEL;
        if (tEdge == null && tEdge1 == null && e != m_SortedEdges)
        {
            return;
        }

        if (tEdge == null)
        {
            m_SortedEdges = tEdge1;
        }
        else
        {
            tEdge.nextInSEL = tEdge1;
        }

        if (tEdge1 != null)
        {
            tEdge1.prevInSEL = tEdge;
        }

        e.nextInSEL = null;
        e.prevInSEL = null;
    }

    private void DisposeAllPolyPts()
    {
        for (var i = 0; i < mPolyOuts.Count; i++)
        {
            DisposeOutRec(i);
        }

        mPolyOuts.Clear();
    }

    private void DisposeIntersectNodes()
    {
        while (m_IntersectNodes != null)
        {
            var mIntersectNodes = m_IntersectNodes.next;
            m_IntersectNodes = null;
            m_IntersectNodes = mIntersectNodes;
        }
    }

    private void DisposeOutPts(OutPt pp)
    {
        if (pp == null)
        {
            return;
        }

        pp.prev.next = null;
        while (pp != null)
        {
            pp = pp.next;
        }
    }

    private void DisposeOutRec(int index)
    {
        var item = mPolyOuts[index];
        if (item.pts != null)
        {
            DisposeOutPts(item.pts);
        }

        item = null;
        mPolyOuts[index] = null;
    }

    private void DisposeScanbeamList()
    {
        while (m_Scanbeam != null)
        {
            var mScanbeam = m_Scanbeam.next;
            m_Scanbeam = null;
            m_Scanbeam = mScanbeam;
        }
    }

    private void DoBothEdges(TEdge edge1, TEdge edge2, Coordinate pt)
    {
        AddOutPt(edge1, pt);
        AddOutPt(edge2, pt);
        SwapSides(edge1, edge2);
        SwapPolyIndexes(edge1, edge2);
    }

    private void DoEdge1(TEdge edge1, TEdge edge2, Coordinate pt)
    {
        AddOutPt(edge1, pt);
        SwapSides(edge1, edge2);
        SwapPolyIndexes(edge1, edge2);
    }

    private void DoEdge2(TEdge edge1, TEdge edge2, Coordinate pt)
    {
        AddOutPt(edge2, pt);
        SwapSides(edge1, edge2);
        SwapPolyIndexes(edge1, edge2);
    }

    private void DoMaxima(TEdge e, long topY)
    {
        var maximaPair = GetMaximaPair(e);
        var num = e.xtop;
        for (var i = e.nextInAEL; i != maximaPair; i = i.nextInAEL)
        {
            if (i == null)
            {
                throw new ClipperException("DoMaxima error");
            }

            IntersectEdges(
                e, i,
                new Coordinate
                {
                    X = num,
                    Y = topY
                },
                Protects.ipBoth);
            SwapPositionsInAEL(e, i);
        }

        if (e.outIdx < 0 && maximaPair.outIdx < 0)
        {
            DeleteFromAEL(e);
            DeleteFromAEL(maximaPair);
            return;
        }

        if (e.outIdx < 0 || maximaPair.outIdx < 0)
        {
            throw new ClipperException("DoMaxima error");
        }

        IntersectEdges(e, maximaPair, new Coordinate {X = num, Y = topY}, Protects.ipNone);
    }

    private bool E2InsertsBeforeE1(TEdge e1, TEdge e2)
    {
        if (e2.xcurr != e1.xcurr)
        {
            return e2.xcurr < e1.xcurr;
        }

        return e2.dx > e1.dx;
    }

    public bool Execute(ClipType clipType, List<List<Coordinate>> solution, PolyFillType subjFillType, PolyFillType clipFillType)
    {
        if (m_ExecuteLocked)
        {
            return false;
        }

        m_ExecuteLocked = true;
        solution.Clear();
        m_SubjFillType = subjFillType;
        m_ClipFillType = clipFillType;
        mClipType = clipType;
        m_UsingPolyTree = false;
        var flag = ExecuteInternal();
        if (flag)
        {
            BuildResult(solution);
        }

        m_ExecuteLocked = false;
        return flag;
    }

    public bool Execute(ClipType clipType, PolyTree polytree, PolyFillType subjFillType, PolyFillType clipFillType)
    {
        if (m_ExecuteLocked)
        {
            return false;
        }

        m_ExecuteLocked = true;
        m_SubjFillType = subjFillType;
        m_ClipFillType = clipFillType;
        mClipType = clipType;
        m_UsingPolyTree = true;
        var flag = ExecuteInternal();
        if (flag)
        {
            BuildResult2(polytree);
        }

        m_ExecuteLocked = false;
        return flag;
    }

    public bool Execute(ClipType clipType, List<List<Coordinate>> solution)
    {
        return Execute(clipType, solution, PolyFillType.pftEvenOdd, PolyFillType.pftEvenOdd);
    }

    public bool Execute(ClipType clipType, PolyTree polytree)
    {
        return Execute(clipType, polytree, PolyFillType.pftEvenOdd, PolyFillType.pftEvenOdd);
    }

    private bool ExecuteInternal()
    {
        bool flag;
        try
        {
            Reset();
            if (m_CurrentLM != null)
            {
                var num = PopScanbeam();
                do
                {
                    InsertLocalMinimaIntoAEL(num);
                    mHorizJoins.Clear();
                    ProcessHorizontals();
                    var num1 = PopScanbeam();
                    flag = ProcessIntersections(num, num1);
                    if (!flag)
                    {
                        break;
                    }

                    ProcessEdgesAtTopOfScanbeam(num1);
                    num = num1;
                } while (m_Scanbeam != null);
            }
            else
            {
                return true;
            }
        }
        catch
        {
            flag = false;
        }

        if (flag)
        {
            for (var i = 0; i < mPolyOuts.Count; i++)
            {
                var item = mPolyOuts[i];
                if (item.pts != null)
                {
                    FixupOutPolygon(item);
                    if (item.pts != null && (item.isHole ^ mReverseOutput) == Area(item, m_UseFullRange) > 0)
                    {
                        ReversePolyPtLinks(item.pts);
                    }
                }
            }

            JoinCommonEdges();
        }

        mJoins.Clear();
        mHorizJoins.Clear();
        return flag;
    }

    private bool FindSegment(ref OutPt pp, ref Coordinate pt1, ref Coordinate pt2)
    {
        if (pp == null)
        {
            return false;
        }

        var outPt = pp;
        var intPoint = new Coordinate
        {
            X = pt1.X,
            Y = pt1.Y,
        };
        var intPoint1 = new Coordinate
        {
            X = pt2.X,
            Y = pt2.Y,
        };
        do
        {
            if (base.SlopesEqual(intPoint, intPoint1, pp.pt, pp.prev.pt, true) && base.SlopesEqual(intPoint, intPoint1, pp.pt, true) && this.GetOverlapSegment(intPoint, intPoint1, pp.pt, pp.prev.pt, ref pt1, ref pt2))
            {
                return true;
            }

            pp = pp.next;
        } while (pp != outPt);

        return false;
    }

    private bool FirstIsBottomPt(OutPt btmPt1, OutPt btmPt2)
    {
        var outPt = btmPt1.prev;
        while (PointsEqual(outPt.pt, btmPt1.pt) && outPt != btmPt1)
        {
            outPt = outPt.prev;
        }

        var num = Math.Abs(this.GetDx(btmPt1.pt, outPt.pt));
        outPt = btmPt1.next;
        while (PointsEqual(outPt.pt, btmPt1.pt) && outPt != btmPt1)
        {
            outPt = outPt.next;
        }

        var num1 = Math.Abs(this.GetDx(btmPt1.pt, outPt.pt));
        outPt = btmPt2.prev;
        while (PointsEqual(outPt.pt, btmPt2.pt) && outPt != btmPt2)
        {
            outPt = outPt.prev;
        }

        var num2 = Math.Abs(this.GetDx(btmPt2.pt, outPt.pt));
        outPt = btmPt2.next;
        while (PointsEqual(outPt.pt, btmPt2.pt) && outPt != btmPt2)
        {
            outPt = outPt.next;
        }

        var num3 = Math.Abs(this.GetDx(btmPt2.pt, outPt.pt));
        if (num >= num2 && num >= num3)
        {
            return true;
        }

        if (num1 < num2)
        {
            return false;
        }

        return num1 >= num3;
    }

    internal void FixHoleLinkage(OutRec outRec)
    {
        if (outRec.FirstLeft == null || outRec.isHole != outRec.FirstLeft.isHole && outRec.FirstLeft.pts != null)
        {
            return;
        }

        var firstLeft = outRec.FirstLeft;
        while (firstLeft != null && (firstLeft.isHole == outRec.isHole || firstLeft.pts == null))
        {
            firstLeft = firstLeft.FirstLeft;
        }

        outRec.FirstLeft = firstLeft;
    }

    private void FixupFirstLefts1(OutRec OldOutRec, OutRec NewOutRec)
    {
        for (var i = 0; i < mPolyOuts.Count; i++)
        {
            var item = mPolyOuts[i];
            if (item.pts != null && item.FirstLeft == OldOutRec && Poly2ContainsPoly1(item.pts, NewOutRec.pts))
            {
                item.FirstLeft = NewOutRec;
            }
        }
    }

    private void FixupFirstLefts2(OutRec OldOutRec, OutRec NewOutRec)
    {
        foreach (var mPolyOut in mPolyOuts)
        {
            if (mPolyOut.FirstLeft != OldOutRec)
            {
                continue;
            }

            mPolyOut.FirstLeft = NewOutRec;
        }
    }

    private bool FixupIntersections()
    {
        TEdge tEdge;
        if (m_IntersectNodes.next == null)
        {
            return true;
        }

        CopyAELToSEL();
        var mIntersectNodes = m_IntersectNodes;
        for (var i = m_IntersectNodes.next; i != null; i = mIntersectNodes.next)
        {
            var tEdge1 = mIntersectNodes.edge1;
            if (tEdge1.prevInSEL == mIntersectNodes.edge2)
            {
                tEdge = tEdge1.prevInSEL;
            }
            else if (tEdge1.nextInSEL != mIntersectNodes.edge2)
            {
                while (i != null && i.edge1.nextInSEL != i.edge2 && i.edge1.prevInSEL != i.edge2)
                {
                    i = i.next;
                }

                if (i == null)
                {
                    return false;
                }

                SwapIntersectNodes(mIntersectNodes, i);
                tEdge1 = mIntersectNodes.edge1;
                tEdge = mIntersectNodes.edge2;
            }
            else
            {
                tEdge = tEdge1.nextInSEL;
            }

            SwapPositionsInSEL(tEdge1, tEdge);
            mIntersectNodes = mIntersectNodes.next;
        }

        m_SortedEdges = null;
        if (mIntersectNodes.edge1.prevInSEL == mIntersectNodes.edge2)
        {
            return true;
        }

        return mIntersectNodes.edge1.nextInSEL == mIntersectNodes.edge2;
    }

    private void FixupJoinRecs(JoinRec j, OutPt pt, int startIdx)
    {
        for (var i = startIdx; i < mJoins.Count; i++)
        {
            var item = mJoins[i];
            if (item.poly1Idx == j.poly1Idx && base.PointIsVertex(item.pt1a, pt))
            {
                item.poly1Idx = j.poly2Idx;
            }

            if (item.poly2Idx == j.poly1Idx && base.PointIsVertex(item.pt2a, pt))
            {
                item.poly2Idx = j.poly2Idx;
            }
        }
    }

    private void FixupOutPolygon(OutRec outRec)
    {
        OutPt outPt = null;
        outRec.pts = outRec.bottomPt;
        var outPt1 = outRec.bottomPt;
        while (outPt1.prev != outPt1 && outPt1.prev != outPt1.next)
        {
            if (PointsEqual(outPt1.pt, outPt1.next.pt) || SlopesEqual(outPt1.prev.pt, outPt1.pt, outPt1.next.pt, m_UseFullRange))
            {
                outPt = null;
                if (outPt1 == outRec.bottomPt)
                {
                    outRec.bottomPt = null;
                }

                outPt1.prev.next = outPt1.next;
                outPt1.next.prev = outPt1.prev;
                outPt1 = outPt1.prev;
            }
            else
            {
                if (outPt1 == outPt)
                {
                    if (outRec.bottomPt == null)
                    {
                        outRec.bottomPt = GetBottomPt(outPt1);
                        outRec.bottomPt.idx = outRec.idx;
                        outRec.pts = outRec.bottomPt;
                    }

                    return;
                }

                if (outPt == null)
                {
                    outPt = outPt1;
                }

                outPt1 = outPt1.next;
            }
        }

        DisposeOutPts(outPt1);
        outRec.pts = null;
        outRec.bottomPt = null;
    }

    private static bool FullRangeNeeded(List<Coordinate> pts)
    {
        var flag = false;
        for (var i = 0; i < pts.Count; i++)
        {
            if (Math.Abs(pts[i].X) > 4611686018427387903L || Math.Abs(pts[i].Y) > 4611686018427387903L)
            {
                throw new ClipperException("Coordinate exceeds range bounds.");
            }

            if (Math.Abs(pts[i].X) > 1073741823 || Math.Abs(pts[i].Y) > 1073741823)
            {
                flag = true;
            }
        }

        return flag;
    }

    private OutPt GetBottomPt(OutPt pp)
    {
        OutPt i;
        OutPt outPt = null;
        for (i = pp.next; i != pp; i = i.next)
        {
            if (i.pt.Y > pp.pt.Y)
            {
                pp = i;
                outPt = null;
            }
            else if (i.pt.Y == pp.pt.Y && i.pt.X <= pp.pt.X)
            {
                if (i.pt.X < pp.pt.X)
                {
                    outPt = null;
                    pp = i;
                }
                else if (i.next != pp && i.prev != pp)
                {
                    outPt = i;
                }
            }
        }

        if (outPt != null)
        {
            while (outPt != i)
            {
                if (!FirstIsBottomPt(i, outPt))
                {
                    pp = outPt;
                }

                outPt = outPt.next;
                while (!PointsEqual(outPt.pt, pp.pt))
                {
                    outPt = outPt.next;
                }
            }
        }

        return pp;
    }

    private double GetDx(Coordinate pt1, Coordinate pt2)
    {
        if (pt1.Y == pt2.Y)
        {
            return -3.4E+38;
        }

        return (pt2.X - pt1.X) / (double)(pt2.Y - pt1.Y);
    }

    private OutRec GetLowermostRec(OutRec outRec1, OutRec outRec2)
    {
        var outPt = outRec1.bottomPt;
        var outPt1 = outRec2.bottomPt;
        if (outPt.pt.Y > outPt1.pt.Y)
        {
            return outRec1;
        }

        if (outPt.pt.Y < outPt1.pt.Y)
        {
            return outRec2;
        }

        if (outPt.pt.X < outPt1.pt.X)
        {
            return outRec1;
        }

        if (outPt.pt.X > outPt1.pt.X)
        {
            return outRec2;
        }

        if (outPt.next == outPt)
        {
            return outRec2;
        }

        if (outPt1.next == outPt1)
        {
            return outRec1;
        }

        if (FirstIsBottomPt(outPt, outPt1))
        {
            return outRec1;
        }

        return outRec2;
    }

    private TEdge GetMaximaPair(TEdge e)
    {
        if (IsMaxima(e.next, e.ytop) && e.next.xtop == e.xtop)
        {
            return e.next;
        }

        return e.prev;
    }

    private TEdge GetNextInAEL(TEdge e, Direction Direction)
    {
        if (Direction != Direction.dLeftToRight)
        {
            return e.prevInAEL;
        }

        return e.nextInAEL;
    }

    private bool GetOverlapSegment(Coordinate pt1a, Coordinate pt1b, Coordinate pt2a, Coordinate pt2b, ref Coordinate pt1, ref Coordinate pt2)
    {
        if (Math.Abs(pt1a.X - pt1b.X) > Math.Abs(pt1a.Y - pt1b.Y))
        {
            if (pt1a.X > pt1b.X)
            {
                SwapPoints(ref pt1a, ref pt1b);
            }

            if (pt2a.X > pt2b.X)
            {
                SwapPoints(ref pt2a, ref pt2b);
            }

            if (pt1a.X <= pt2a.X)
            {
                pt1 = pt2a;
            }
            else
            {
                pt1 = pt1a;
            }

            if (pt1b.X >= pt2b.X)
            {
                pt2 = pt2b;
            }
            else
            {
                pt2 = pt1b;
            }

            return pt1.X < pt2.X;
        }

        if (pt1a.Y < pt1b.Y)
        {
            SwapPoints(ref pt1a, ref pt1b);
        }

        if (pt2a.Y < pt2b.Y)
        {
            SwapPoints(ref pt2a, ref pt2b);
        }

        if (pt1a.Y >= pt2a.Y)
        {
            pt1 = pt2a;
        }
        else
        {
            pt1 = pt1a;
        }

        if (pt1b.Y <= pt2b.Y)
        {
            pt2 = pt2b;
        }
        else
        {
            pt2 = pt1b;
        }

        return pt1.Y > pt2.Y;
    }

    internal static DoublePoint GetUnitNormal(Coordinate pt1, Coordinate pt2)
    {
        var x = (double)(pt2.X - pt1.X);
        var y = (double)(pt2.Y - pt1.Y);
        if (x == 0 && y == 0)
        {
            return new DoublePoint(0, 0);
        }

        var num = 1 / Math.Sqrt(x * x + y * y);
        x *= num;
        y *= num;
        return new DoublePoint(y, -x);
    }

    private void InsertEdgeIntoAEL(TEdge edge)
    {
        edge.prevInAEL = null;
        edge.nextInAEL = null;
        if (m_ActiveEdges == null)
        {
            m_ActiveEdges = edge;
            return;
        }

        if (E2InsertsBeforeE1(m_ActiveEdges, edge))
        {
            edge.nextInAEL = m_ActiveEdges;
            m_ActiveEdges.prevInAEL = edge;
            m_ActiveEdges = edge;
            return;
        }

        var mActiveEdges = m_ActiveEdges;
        while (mActiveEdges.nextInAEL != null && !E2InsertsBeforeE1(mActiveEdges.nextInAEL, edge))
        {
            mActiveEdges = mActiveEdges.nextInAEL;
        }

        edge.nextInAEL = mActiveEdges.nextInAEL;
        if (mActiveEdges.nextInAEL != null)
        {
            mActiveEdges.nextInAEL.prevInAEL = edge;
        }

        edge.prevInAEL = mActiveEdges;
        mActiveEdges.nextInAEL = edge;
    }

    private void InsertLocalMinimaIntoAEL(long botY)
    {
        while (m_CurrentLM != null && m_CurrentLM.Y == botY)
        {
            var mCurrentLM = m_CurrentLM.leftBound;
            var tEdge = m_CurrentLM.rightBound;
            InsertEdgeIntoAEL(mCurrentLM);
            InsertScanbeam(mCurrentLM.ytop);
            InsertEdgeIntoAEL(tEdge);
            if (!IsEvenOddFillType(mCurrentLM))
            {
                tEdge.windDelta = -mCurrentLM.windDelta;
            }
            else
            {
                mCurrentLM.windDelta = 1;
                tEdge.windDelta = 1;
            }

            SetWindingCount(mCurrentLM);
            tEdge.windCnt = mCurrentLM.windCnt;
            tEdge.windCnt2 = mCurrentLM.windCnt2;
            if (tEdge.dx != -3.4E+38)
            {
                InsertScanbeam(tEdge.ytop);
            }
            else
            {
                AddEdgeToSEL(tEdge);
                InsertScanbeam(tEdge.nextInLML.ytop);
            }

            if (IsContributing(mCurrentLM))
            {
                AddLocalMinPoly(mCurrentLM, tEdge, new Coordinate {X = mCurrentLM.xcurr, Y = m_CurrentLM.Y});
            }

            if (tEdge.outIdx >= 0 && tEdge.dx == -3.4E+38)
            {
                for (var i = 0; i < mHorizJoins.Count; i++)
                {
                    var intPoint = new Coordinate();
                    var intPoint1 = new Coordinate();
                    var item = mHorizJoins[i];
                    if (GetOverlapSegment(
                        new Coordinate {X = item.edge.xbot, Y = item.edge.ybot},
                        new Coordinate {X = item.edge.xtop, Y = item.edge.ytop},
                        new Coordinate {X = tEdge.xbot, Y = tEdge.ybot},
                        new Coordinate {X = tEdge.xtop, Y = tEdge.ytop},
                        ref intPoint, ref intPoint1))
                    {
                        AddJoin(item.edge, tEdge, item.savedIdx, -1);
                    }
                }
            }

            if (mCurrentLM.nextInAEL != tEdge)
            {
                if (tEdge.outIdx >= 0 && tEdge.prevInAEL.outIdx >= 0 && SlopesEqual(tEdge.prevInAEL, tEdge, m_UseFullRange))
                {
                    AddJoin(tEdge, tEdge.prevInAEL, -1, -1);
                }

                var tEdge1 = mCurrentLM.nextInAEL;
                var intPoint2 = new Coordinate
                {
                    X = mCurrentLM.xcurr,
                    Y = mCurrentLM.ycurr
                };
                while (tEdge1 != tEdge)
                {
                    if (tEdge1 == null)
                    {
                        throw new ClipperException("InsertLocalMinimaIntoAEL: missing rightbound!");
                    }

                    IntersectEdges(tEdge, tEdge1, intPoint2, Protects.ipNone);
                    tEdge1 = tEdge1.nextInAEL;
                }
            }

            PopLocalMinima();
        }
    }

    private OutPt InsertPolyPtBetween(OutPt p1, OutPt p2, Coordinate pt)
    {
        var outPt = new OutPt()
        {
            pt = pt
        };
        if (p2 != p1.next)
        {
            p2.next = outPt;
            p1.prev = outPt;
            outPt.next = p1;
            outPt.prev = p2;
        }
        else
        {
            p1.next = outPt;
            p2.prev = outPt;
            outPt.next = p2;
            outPt.prev = p1;
        }

        return outPt;
    }

    private void InsertScanbeam(long Y)
    {
        if (m_Scanbeam == null)
        {
            m_Scanbeam = new Scanbeam()
            {
                next = null,
                Y = Y
            };
            return;
        }

        if (Y > m_Scanbeam.Y)
        {
            var scanbeam = new Scanbeam()
            {
                Y = Y,
                next = m_Scanbeam
            };
            m_Scanbeam = scanbeam;
            return;
        }

        var mScanbeam = m_Scanbeam;
        while (mScanbeam.next != null && Y <= mScanbeam.next.Y)
        {
            mScanbeam = mScanbeam.next;
        }

        if (Y == mScanbeam.Y)
        {
            return;
        }

        var scanbeam1 = new Scanbeam()
        {
            Y = Y,
            next = mScanbeam.next
        };
        mScanbeam.next = scanbeam1;
    }

    private void IntersectEdges(TEdge e1, TEdge e2, Coordinate pt, Protects protects)
    {
        PolyFillType mClipFillType;
        PolyFillType mSubjFillType;
        PolyFillType polyFillType;
        PolyFillType mSubjFillType1;
        int num;
        int num1;
        long num2;
        long num3;
        var flag = ((Protects.ipLeft & protects) != Protects.ipNone || e1.nextInLML != null || e1.xtop != pt.X ? false : e1.ytop == pt.Y);
        var flag1 = ((Protects.ipRight & protects) != Protects.ipNone || e2.nextInLML != null || e2.xtop != pt.X ? false : e2.ytop == pt.Y);
        var flag2 = e1.outIdx >= 0;
        var flag3 = e2.outIdx >= 0;
        if (e1.polyType != e2.polyType)
        {
            if (IsEvenOddFillType(e2))
            {
                e1.windCnt2 = (e1.windCnt2 == 0 ? 1 : 0);
            }
            else
            {
                e1.windCnt2 += e2.windDelta;
            }

            if (IsEvenOddFillType(e1))
            {
                e2.windCnt2 = (e2.windCnt2 == 0 ? 1 : 0);
            }
            else
            {
                e2.windCnt2 -= e1.windDelta;
            }
        }
        else if (!IsEvenOddFillType(e1))
        {
            if (e1.windCnt + e2.windDelta != 0)
            {
                e1.windCnt += e2.windDelta;
            }
            else
            {
                e1.windCnt = -e1.windCnt;
            }

            if (e2.windCnt - e1.windDelta != 0)
            {
                e2.windCnt -= e1.windDelta;
            }
            else
            {
                e2.windCnt = -e2.windCnt;
            }
        }
        else
        {
            var num4 = e1.windCnt;
            e1.windCnt = e2.windCnt;
            e2.windCnt = num4;
        }

        if (e1.polyType != PolyType.ptSubject)
        {
            mClipFillType = m_ClipFillType;
            polyFillType = m_SubjFillType;
        }
        else
        {
            mClipFillType = m_SubjFillType;
            polyFillType = m_ClipFillType;
        }

        if (e2.polyType != PolyType.ptSubject)
        {
            mSubjFillType = m_ClipFillType;
            mSubjFillType1 = m_SubjFillType;
        }
        else
        {
            mSubjFillType = m_SubjFillType;
            mSubjFillType1 = m_ClipFillType;
        }

        switch (mClipFillType)
        {
            case PolyFillType.pftPositive:
            {
                num = e1.windCnt;
                break;
            }
            case PolyFillType.pftNegative:
            {
                num = -e1.windCnt;
                break;
            }
            default:
            {
                num = Math.Abs(e1.windCnt);
                break;
            }
        }

        switch (mSubjFillType)
        {
            case PolyFillType.pftPositive:
            {
                num1 = e2.windCnt;
                break;
            }
            case PolyFillType.pftNegative:
            {
                num1 = -e2.windCnt;
                break;
            }
            default:
            {
                num1 = Math.Abs(e2.windCnt);
                break;
            }
        }

        if (flag2 && flag3)
        {
            if (flag || flag1 || num != 0 && num != 1 || num1 != 0 && num1 != 1 || e1.polyType != e2.polyType && mClipType != ClipType.ctXor)
            {
                AddLocalMaxPoly(e1, e2, pt);
            }
            else
            {
                DoBothEdges(e1, e2, pt);
            }
        }
        else if (flag2)
        {
            if ((num1 == 0 || num1 == 1) && (mClipType != ClipType.ctIntersection || e2.polyType == PolyType.ptSubject || e2.windCnt2 != 0))
            {
                DoEdge1(e1, e2, pt);
            }
        }
        else if (flag3)
        {
            if ((num == 0 || num == 1) && (mClipType != ClipType.ctIntersection || e1.polyType == PolyType.ptSubject || e1.windCnt2 != 0))
            {
                DoEdge2(e1, e2, pt);
            }
        }
        else if ((num == 0 || num == 1) && (num1 == 0 || num1 == 1) && !flag && !flag1)
        {
            switch (polyFillType)
            {
                case PolyFillType.pftPositive:
                {
                    num2 = e1.windCnt2;
                    break;
                }
                case PolyFillType.pftNegative:
                {
                    num2 = -e1.windCnt2;
                    break;
                }
                default:
                {
                    num2 = Math.Abs(e1.windCnt2);
                    break;
                }
            }

            switch (mSubjFillType1)
            {
                case PolyFillType.pftPositive:
                {
                    num3 = e2.windCnt2;
                    break;
                }
                case PolyFillType.pftNegative:
                {
                    num3 = -e2.windCnt2;
                    break;
                }
                default:
                {
                    num3 = Math.Abs(e2.windCnt2);
                    break;
                }
            }

            if (e1.polyType != e2.polyType)
            {
                AddLocalMinPoly(e1, e2, pt);
            }
            else if (num != 1 || num1 != 1)
            {
                SwapSides(e1, e2);
            }
            else
            {
                switch (mClipType)
                {
                    case ClipType.ctIntersection:
                    {
                        if (num2 <= 0 || num3 <= 0)
                        {
                            break;
                        }

                        AddLocalMinPoly(e1, e2, pt);
                        break;
                    }
                    case ClipType.ctUnion:
                    {
                        if (num2 > 0 || num3 > 0)
                        {
                            break;
                        }

                        AddLocalMinPoly(e1, e2, pt);
                        break;
                    }
                    case ClipType.ctDifference:
                    {
                        if ((e1.polyType != PolyType.ptClip || num2 <= 0 || num3 <= 0) && (e1.polyType != PolyType.ptSubject || num2 > 0 || num3 > 0))
                        {
                            break;
                        }

                        AddLocalMinPoly(e1, e2, pt);
                        break;
                    }
                    case ClipType.ctXor:
                    {
                        AddLocalMinPoly(e1, e2, pt);
                        break;
                    }
                }
            }
        }

        if (flag != flag1 && (flag && e1.outIdx >= 0 || flag1 && e2.outIdx >= 0))
        {
            SwapSides(e1, e2);
            SwapPolyIndexes(e1, e2);
        }

        if (flag)
        {
            DeleteFromAEL(e1);
        }

        if (flag1)
        {
            DeleteFromAEL(e2);
        }
    }

    private bool IntersectPoint(TEdge edge1, TEdge edge2, ref Coordinate ip)
    {
        double num;
        double num1;
        if (SlopesEqual(edge1, edge2, m_UseFullRange))
        {
            return false;
        }

        if (edge1.dx == 0)
        {
            ip.X = edge1.xbot;
            if (edge2.dx != -3.4E+38)
            {
                num1 = edge2.ybot - edge2.xbot / edge2.dx;
                ip.Y = Round(ip.X / edge2.dx + num1);
            }
            else
            {
                ip.Y = edge2.ybot;
            }
        }
        else if (edge2.dx != 0)
        {
            num = edge1.xbot - edge1.ybot * edge1.dx;
            num1 = edge2.xbot - edge2.ybot * edge2.dx;
            var num2 = (num1 - num) / (edge1.dx - edge2.dx);
            ip.Y = Round(num2);
            if (Math.Abs(edge1.dx) >= Math.Abs(edge2.dx))
            {
                ip.X = Round(edge2.dx * num2 + num1);
            }
            else
            {
                ip.X = Round(edge1.dx * num2 + num);
            }
        }
        else
        {
            ip.X = edge2.xbot;
            if (edge1.dx != -3.4E+38)
            {
                num = edge1.ybot - edge1.xbot / edge1.dx;
                ip.Y = Round(ip.X / edge1.dx + num);
            }
            else
            {
                ip.Y = edge1.ybot;
            }
        }

        if (ip.Y >= edge1.ytop && ip.Y >= edge2.ytop)
        {
            return true;
        }

        if (edge1.ytop > edge2.ytop)
        {
            ip.X = edge1.xtop;
            ip.Y = edge1.ytop;
            return TopX(edge2, edge1.ytop) < edge1.xtop;
        }

        ip.X = edge2.xtop;
        ip.Y = edge2.ytop;
        return TopX(edge1, edge2.ytop) > edge2.xtop;
    }

    private bool IsContributing(TEdge edge)
    {
        PolyFillType mClipFillType;
        PolyFillType mSubjFillType;
        if (edge.polyType != PolyType.ptSubject)
        {
            mClipFillType = m_ClipFillType;
            mSubjFillType = m_SubjFillType;
        }
        else
        {
            mClipFillType = m_SubjFillType;
            mSubjFillType = m_ClipFillType;
        }

        switch (mClipFillType)
        {
            case PolyFillType.pftEvenOdd:
            case PolyFillType.pftNonZero:
            {
                if (Math.Abs(edge.windCnt) == 1)
                {
                    break;
                }

                return false;
            }
            case PolyFillType.pftPositive:
            {
                if (edge.windCnt == 1)
                {
                    break;
                }

                return false;
            }
            default:
            {
                if (edge.windCnt == -1)
                {
                    break;
                }

                return false;
            }
        }

        switch (mClipType)
        {
            case ClipType.ctIntersection:
            {
                switch (mSubjFillType)
                {
                    case PolyFillType.pftEvenOdd:
                    case PolyFillType.pftNonZero:
                    {
                        return edge.windCnt2 != 0;
                    }
                    case PolyFillType.pftPositive:
                    {
                        return edge.windCnt2 > 0;
                    }
                }

                return edge.windCnt2 < 0;
            }
            case ClipType.ctUnion:
            {
                switch (mSubjFillType)
                {
                    case PolyFillType.pftEvenOdd:
                    case PolyFillType.pftNonZero:
                    {
                        return edge.windCnt2 == 0;
                    }
                    case PolyFillType.pftPositive:
                    {
                        return edge.windCnt2 <= 0;
                    }
                }

                return edge.windCnt2 >= 0;
            }
            case ClipType.ctDifference:
            {
                if (edge.polyType != PolyType.ptSubject)
                {
                    switch (mSubjFillType)
                    {
                        case PolyFillType.pftEvenOdd:
                        case PolyFillType.pftNonZero:
                        {
                            return edge.windCnt2 != 0;
                        }
                        case PolyFillType.pftPositive:
                        {
                            return edge.windCnt2 > 0;
                        }
                    }

                    return edge.windCnt2 < 0;
                }

                switch (mSubjFillType)
                {
                    case PolyFillType.pftEvenOdd:
                    case PolyFillType.pftNonZero:
                    {
                        return edge.windCnt2 == 0;
                    }
                    case PolyFillType.pftPositive:
                    {
                        return edge.windCnt2 <= 0;
                    }
                }

                return edge.windCnt2 >= 0;
            }
        }

        return true;
    }

    private bool IsEvenOddAltFillType(TEdge edge)
    {
        if (edge.polyType == PolyType.ptSubject)
        {
            return m_ClipFillType == PolyFillType.pftEvenOdd;
        }

        return m_SubjFillType == PolyFillType.pftEvenOdd;
    }

    private bool IsEvenOddFillType(TEdge edge)
    {
        if (edge.polyType == PolyType.ptSubject)
        {
            return m_SubjFillType == PolyFillType.pftEvenOdd;
        }

        return m_ClipFillType == PolyFillType.pftEvenOdd;
    }

    private bool IsIntermediate(TEdge e, double Y)
    {
        if (e.ytop != Y)
        {
            return false;
        }

        return e.nextInLML != null;
    }

    private bool IsMaxima(TEdge e, double Y)
    {
        if (e == null || e.ytop != Y)
        {
            return false;
        }

        return e.nextInLML == null;
    }

    private bool IsMinima(TEdge e)
    {
        if (e == null || e.prev.nextInLML == e)
        {
            return false;
        }

        return e.next.nextInLML != e;
    }

    private bool IsTopHorz(TEdge horzEdge, double XPos)
    {
        for (var i = m_SortedEdges; i != null; i = i.nextInSEL)
        {
            if (XPos >= Math.Min(i.xcurr, i.xtop) && XPos <= Math.Max(i.xcurr, i.xtop))
            {
                return false;
            }
        }

        return true;
    }

    private void JoinCommonEdges()
    {
        OutRec outRec;
        OutPt outPt;
        OutPt outPt1;
        for (var i = 0; i < mJoins.Count; i++)
        {
            var item = mJoins[i];
            var firstLeft = mPolyOuts[item.poly1Idx];
            var count = mPolyOuts[item.poly2Idx];
            if (firstLeft.pts != null && count.pts != null)
            {
                if (firstLeft == count)
                {
                    outRec = firstLeft;
                }
                else if (!Param1RightOfParam2(firstLeft, count))
                {
                    outRec = (!Param1RightOfParam2(count, firstLeft) ? GetLowermostRec(firstLeft, count) : firstLeft);
                }
                else
                {
                    outRec = count;
                }

                if (JoinPoints(item, out outPt, out outPt1))
                {
                    if (firstLeft != count)
                    {
                        FixupOutPolygon(firstLeft);
                        var num = firstLeft.idx;
                        var num1 = count.idx;
                        count.pts = null;
                        count.bottomPt = null;
                        firstLeft.isHole = outRec.isHole;
                        if (outRec == count)
                        {
                            firstLeft.FirstLeft = count.FirstLeft;
                        }

                        count.FirstLeft = firstLeft;
                        for (var j = i + 1; j < mJoins.Count; j++)
                        {
                            var joinRec = mJoins[j];
                            if (joinRec.poly1Idx == num1)
                            {
                                joinRec.poly1Idx = num;
                            }

                            if (joinRec.poly2Idx == num1)
                            {
                                joinRec.poly2Idx = num;
                            }
                        }

                        if (m_UsingPolyTree)
                        {
                            FixupFirstLefts2(count, firstLeft);
                        }
                    }
                    else
                    {
                        firstLeft.pts = GetBottomPt(outPt);
                        firstLeft.bottomPt = firstLeft.pts;
                        firstLeft.bottomPt.idx = firstLeft.idx;
                        count = CreateOutRec();
                        mPolyOuts.Add(count);
                        count.idx = mPolyOuts.Count - 1;
                        item.poly2Idx = count.idx;
                        count.pts = GetBottomPt(outPt1);
                        count.bottomPt = count.pts;
                        count.bottomPt.idx = count.idx;
                        if (Poly2ContainsPoly1(count.pts, firstLeft.pts))
                        {
                            count.isHole = !firstLeft.isHole;
                            count.FirstLeft = firstLeft;
                            FixupJoinRecs(item, outPt1, i + 1);
                            if (m_UsingPolyTree)
                            {
                                FixupFirstLefts2(count, firstLeft);
                            }

                            FixupOutPolygon(firstLeft);
                            FixupOutPolygon(count);
                            if ((count.isHole ^ mReverseOutput) == Area(count, m_UseFullRange) > 0)
                            {
                                ReversePolyPtLinks(count.pts);
                            }
                        }
                        else if (!Poly2ContainsPoly1(firstLeft.pts, count.pts))
                        {
                            count.isHole = firstLeft.isHole;
                            count.FirstLeft = firstLeft.FirstLeft;
                            FixupJoinRecs(item, outPt1, i + 1);
                            if (m_UsingPolyTree)
                            {
                                FixupFirstLefts1(firstLeft, count);
                            }

                            FixupOutPolygon(firstLeft);
                            FixupOutPolygon(count);
                        }
                        else
                        {
                            count.isHole = firstLeft.isHole;
                            firstLeft.isHole = !count.isHole;
                            count.FirstLeft = firstLeft.FirstLeft;
                            firstLeft.FirstLeft = count;
                            FixupJoinRecs(item, outPt1, i + 1);
                            if (m_UsingPolyTree)
                            {
                                FixupFirstLefts2(firstLeft, count);
                            }

                            FixupOutPolygon(firstLeft);
                            FixupOutPolygon(count);
                            if ((firstLeft.isHole ^ mReverseOutput) == Area(firstLeft, m_UseFullRange) > 0)
                            {
                                ReversePolyPtLinks(firstLeft.pts);
                            }
                        }
                    }
                }
            }
        }
    }

    private bool JoinPoints(JoinRec j, out OutPt p1, out OutPt p2)
    {
        OutPt outPt;
        OutPt outPt1;
        p1 = null;
        p2 = null;
        var item = mPolyOuts[j.poly1Idx];
        var outRec = mPolyOuts[j.poly2Idx];
        if (item == null || outRec == null)
        {
            return false;
        }

        var outPt2 = item.pts;
        var outPt3 = outRec.pts;
        var intPoint = j.pt2a;
        var intPoint1 = j.pt2b;
        var intPoint2 = j.pt1a;
        var intPoint3 = j.pt1b;
        if (!FindSegment(ref outPt2, ref intPoint, ref intPoint1))
        {
            return false;
        }

        if (item == outRec)
        {
            outPt3 = outPt2.next;
            if (!FindSegment(ref outPt3, ref intPoint2, ref intPoint3) || outPt3 == outPt2)
            {
                return false;
            }
        }
        else if (!FindSegment(ref outPt3, ref intPoint2, ref intPoint3))
        {
            return false;
        }

        if (!GetOverlapSegment(intPoint, intPoint1, intPoint2, intPoint3, ref intPoint, ref intPoint1))
        {
            return false;
        }

        var outPt4 = outPt2.prev;
        if (ClipperBase.PointsEqual(outPt2.pt, intPoint))
        {
            p1 = outPt2;
        }
        else if (!ClipperBase.PointsEqual(outPt4.pt, intPoint))
        {
            p1 = InsertPolyPtBetween(outPt2, outPt4, intPoint);
        }
        else
        {
            p1 = outPt4;
        }

        if (ClipperBase.PointsEqual(outPt2.pt, intPoint1))
        {
            p2 = outPt2;
        }
        else if (ClipperBase.PointsEqual(outPt4.pt, intPoint1))
        {
            p2 = outPt4;
        }
        else if (p1 == outPt2 || p1 == outPt4)
        {
            p2 = InsertPolyPtBetween(outPt2, outPt4, intPoint1);
        }
        else if (!this.Pt3IsBetweenPt1AndPt2(outPt2.pt, p1.pt, intPoint1))
        {
            p2 = InsertPolyPtBetween(p1, outPt4, intPoint1);
        }
        else
        {
            p2 = InsertPolyPtBetween(outPt2, p1, intPoint1);
        }

        outPt4 = outPt3.prev;
        if (!ClipperBase.PointsEqual(outPt3.pt, intPoint))
        {
            outPt = (!ClipperBase.PointsEqual(outPt4.pt, intPoint) ? InsertPolyPtBetween(outPt3, outPt4, intPoint) : outPt4);
        }
        else
        {
            outPt = outPt3;
        }

        if (ClipperBase.PointsEqual(outPt3.pt, intPoint1))
        {
            outPt1 = outPt3;
        }
        else if (ClipperBase.PointsEqual(outPt4.pt, intPoint1))
        {
            outPt1 = outPt4;
        }
        else if (outPt == outPt3 || outPt == outPt4)
        {
            outPt1 = InsertPolyPtBetween(outPt3, outPt4, intPoint1);
        }
        else
        {
            outPt1 = (!this.Pt3IsBetweenPt1AndPt2(outPt3.pt, outPt.pt, intPoint1) ? InsertPolyPtBetween(outPt, outPt4, intPoint1) : InsertPolyPtBetween(outPt3, outPt, intPoint1));
        }

        if (p1.next == p2 && outPt.prev == outPt1)
        {
            p1.next = outPt;
            outPt.prev = p1;
            p2.prev = outPt1;
            outPt1.next = p2;
            return true;
        }

        if (p1.prev != p2 || outPt.next != outPt1)
        {
            return false;
        }

        p1.prev = outPt;
        outPt.next = p1;
        p2.next = outPt1;
        outPt1.prev = p2;
        return true;
    }

    public static List<List<Coordinate>> OffsetPolygons(List<List<Coordinate>> poly, double delta, JoinType jointype, double MiterLimit, bool AutoFix)
    {
        var lists = new List<List<Coordinate>>(poly.Count);
        var polyOffsetBuilder = new PolyOffsetBuilder(poly, lists, delta, jointype, MiterLimit, AutoFix);
        return lists;
    }

    public static List<List<Coordinate>> OffsetPolygons(List<List<Coordinate>> poly, double delta, JoinType jointype, double MiterLimit)
    {
        var lists = new List<List<Coordinate>>(poly.Count);
        var polyOffsetBuilder = new PolyOffsetBuilder(poly, lists, delta, jointype, MiterLimit, true);
        return lists;
    }

    public static List<List<Coordinate>> OffsetPolygons(List<List<Coordinate>> poly, double delta, JoinType jointype)
    {
        var lists = new List<List<Coordinate>>(poly.Count);
        var polyOffsetBuilder = new PolyOffsetBuilder(poly, lists, delta, jointype, 2, true);
        return lists;
    }

    public static List<List<Coordinate>> OffsetPolygons(List<List<Coordinate>> poly, double delta)
    {
        var lists = new List<List<Coordinate>>(poly.Count);
        var polyOffsetBuilder = new PolyOffsetBuilder(poly, lists, delta, JoinType.jtSquare, 2, true);
        return lists;
    }

    public static bool Orientation(List<Coordinate> poly)
    {
        return Area(poly) >= 0;
    }

    private bool Param1RightOfParam2(OutRec outRec1, OutRec outRec2)
    {
        do
        {
            outRec1 = outRec1.FirstLeft;
            if (outRec1 != outRec2)
            {
                continue;
            }

            return true;
        } while (outRec1 != null);

        return false;
    }

    private int PointCount(OutPt pts)
    {
        if (pts == null)
        {
            return 0;
        }

        var num = 0;
        var outPt = pts;
        do
        {
            num++;
            outPt = outPt.next;
        } while (outPt != pts);

        return num;
    }

    private static bool Poly2ContainsPoly1(OutPt outPt1, OutPt outPt2)
    {
        var op = outPt1;
        do
        {
            //nb: PointInPolygon returns 0 if false, +1 if true, -1 if pt on polygon
            var res = PointInPolygon(op.pt, outPt2);
            if (res >= 0) return res > 0;
            op = op.next;
        } while (op != outPt1);

        return true;
    }

    public static void PolyTreeToPolygons(PolyTree polytree, List<List<Coordinate>> polygons)
    {
        polygons.Clear();
        polygons.Capacity = polytree.Total;
        AddPolyNodeToPolygons(polytree, polygons);
    }

    private long PopScanbeam()
    {
        var y = m_Scanbeam.Y;
        m_Scanbeam = m_Scanbeam.next;
        return y;
    }

    private void ProcessEdgesAtTopOfScanbeam(long topY)
    {
        var mActiveEdges = m_ActiveEdges;
        while (mActiveEdges != null)
        {
            if (!IsMaxima(mActiveEdges, topY) || GetMaximaPair(mActiveEdges).dx == -3.4E+38)
            {
                if (!IsIntermediate(mActiveEdges, topY) || mActiveEdges.nextInLML.dx != -3.4E+38)
                {
                    mActiveEdges.xcurr = TopX(mActiveEdges, topY);
                    mActiveEdges.ycurr = topY;
                }
                else
                {
                    if (mActiveEdges.outIdx >= 0)
                    {
                        AddOutPt(mActiveEdges, new Coordinate {X = mActiveEdges.xtop, Y = mActiveEdges.ytop});
                        for (var i = 0; i < mHorizJoins.Count; i++)
                        {
                            var intPoint = new Coordinate();
                            var intPoint1 = new Coordinate();
                            var item = mHorizJoins[i];
                            if (GetOverlapSegment(
                                new Coordinate {X = item.edge.xbot, Y = item.edge.ybot},
                                new Coordinate {X = item.edge.xtop, Y = item.edge.ytop},
                                new Coordinate {X = mActiveEdges.nextInLML.xbot, Y = mActiveEdges.nextInLML.ybot},
                                new Coordinate {X = mActiveEdges.nextInLML.xtop, Y = mActiveEdges.nextInLML.ytop},
                                ref intPoint, ref intPoint1))
                            {
                                AddJoin(item.edge, mActiveEdges.nextInLML, item.savedIdx, mActiveEdges.outIdx);
                            }
                        }

                        AddHorzJoin(mActiveEdges.nextInLML, mActiveEdges.outIdx);
                    }

                    UpdateEdgeIntoAEL(ref mActiveEdges);
                    AddEdgeToSEL(mActiveEdges);
                }

                mActiveEdges = mActiveEdges.nextInAEL;
            }
            else
            {
                var tEdge = mActiveEdges.prevInAEL;
                DoMaxima(mActiveEdges, topY);
                mActiveEdges = (tEdge != null ? tEdge.nextInAEL : m_ActiveEdges);
            }
        }

        ProcessHorizontals();
        for (mActiveEdges = m_ActiveEdges; mActiveEdges != null; mActiveEdges = mActiveEdges.nextInAEL)
        {
            if (IsIntermediate(mActiveEdges, topY))
            {
                if (mActiveEdges.outIdx >= 0)
                {
                    AddOutPt(mActiveEdges, new Coordinate
                    {
                        X = mActiveEdges.xtop,
                        Y = mActiveEdges.ytop
                    });
                }

                UpdateEdgeIntoAEL(ref mActiveEdges);
                var tEdge1 = mActiveEdges.prevInAEL;
                var tEdge2 = mActiveEdges.nextInAEL;
                if (tEdge1 != null && tEdge1.xcurr == mActiveEdges.xbot && tEdge1.ycurr == mActiveEdges.ybot && mActiveEdges.outIdx >= 0 && tEdge1.outIdx >= 0 && tEdge1.ycurr > tEdge1.ytop && SlopesEqual(mActiveEdges, tEdge1, m_UseFullRange))
                {
                    AddOutPt(tEdge1, new Coordinate {X = mActiveEdges.xbot, Y = mActiveEdges.ybot});
                    AddJoin(mActiveEdges, tEdge1, -1, -1);
                }
                else if (tEdge2 != null && tEdge2.xcurr == mActiveEdges.xbot && tEdge2.ycurr == mActiveEdges.ybot && mActiveEdges.outIdx >= 0 && tEdge2.outIdx >= 0 && tEdge2.ycurr > tEdge2.ytop && SlopesEqual(mActiveEdges, tEdge2, m_UseFullRange))
                {
                    AddOutPt(tEdge2, new Coordinate {X = mActiveEdges.xbot, Y = mActiveEdges.ybot});
                    AddJoin(mActiveEdges, tEdge2, -1, -1);
                }
            }
        }
    }

    private void ProcessHorizontal(TEdge horzEdge)
    {
        Direction direction;
        long num;
        long num1;
        TEdge maximaPair;
        TEdge nextInAEL = null;
        if (horzEdge.xcurr >= horzEdge.xtop)
        {
            num = horzEdge.xtop;
            num1 = horzEdge.xcurr;
            direction = Direction.dRightToLeft;
        }
        else
        {
            num = horzEdge.xcurr;
            num1 = horzEdge.xtop;
            direction = Direction.dLeftToRight;
        }

        if (horzEdge.nextInLML == null)
        {
            maximaPair = GetMaximaPair(horzEdge);
        }
        else
        {
            maximaPair = null;
        }

        for (var i = GetNextInAEL(horzEdge, direction); i != null; i = nextInAEL)
        {
            nextInAEL = GetNextInAEL(i, direction);
            if (maximaPair != null || direction == Direction.dLeftToRight && i.xcurr <= num1 || direction == Direction.dRightToLeft && i.xcurr >= num)
            {
                if (i.xcurr == horzEdge.xtop && maximaPair == null)
                {
                    if (SlopesEqual(i, horzEdge.nextInLML, m_UseFullRange))
                    {
                        if (horzEdge.outIdx < 0 || i.outIdx < 0)
                        {
                            break;
                        }

                        AddJoin(horzEdge.nextInLML, i, horzEdge.outIdx, -1);
                        break;
                    }
                    else if (i.dx < horzEdge.nextInLML.dx)
                    {
                        break;
                    }
                }

                if (i == maximaPair)
                {
                    if (direction != Direction.dLeftToRight)
                    {
                        IntersectEdges(i, horzEdge, new Coordinate {X = i.xcurr, Y = horzEdge.ycurr}, Protects.ipNone);
                    }
                    else
                    {
                        IntersectEdges(horzEdge, i, new Coordinate {X = i.xcurr, Y = horzEdge.ycurr}, Protects.ipNone);
                    }

                    if (maximaPair.outIdx >= 0)
                    {
                        throw new ClipperException("ProcessHorizontal error");
                    }

                    return;
                }

                if (i.dx == -3.4E+38 && !IsMinima(i) && i.xcurr <= i.xtop)
                {
                    if (direction != Direction.dLeftToRight)
                    {
                        IntersectEdges(i, horzEdge, new Coordinate {X = i.xcurr, Y = horzEdge.ycurr}, (IsTopHorz(horzEdge, i.xcurr) ? Protects.ipRight : Protects.ipBoth));
                    }
                    else
                    {
                        IntersectEdges(horzEdge, i, new Coordinate {X = i.xcurr, Y = horzEdge.ycurr}, (IsTopHorz(horzEdge, i.xcurr) ? Protects.ipLeft : Protects.ipBoth));
                    }
                }
                else if (direction != Direction.dLeftToRight)
                {
                    IntersectEdges(i, horzEdge, new Coordinate {X = i.xcurr, Y = horzEdge.ycurr}, (IsTopHorz(horzEdge, i.xcurr) ? Protects.ipRight : Protects.ipBoth));
                }
                else
                {
                    IntersectEdges(horzEdge, i, new Coordinate {X = i.xcurr, Y = horzEdge.ycurr}, (IsTopHorz(horzEdge, i.xcurr) ? Protects.ipLeft : Protects.ipBoth));
                }

                SwapPositionsInAEL(horzEdge, i);
            }
            else if (direction == Direction.dLeftToRight && i.xcurr > num1 && horzEdge.nextInSEL == null || direction == Direction.dRightToLeft && i.xcurr < num && horzEdge.nextInSEL == null)
            {
                break;
            }
        }

        if (horzEdge.nextInLML != null)
        {
            if (horzEdge.outIdx >= 0)
            {
                AddOutPt(horzEdge, new Coordinate {X = horzEdge.xtop, Y = horzEdge.ytop});
            }

            UpdateEdgeIntoAEL(ref horzEdge);
            return;
        }

        if (horzEdge.outIdx >= 0)
        {
            IntersectEdges(horzEdge, maximaPair, new Coordinate {X = horzEdge.xtop, Y = horzEdge.ycurr}, Protects.ipBoth);
        }

        DeleteFromAEL(maximaPair);
        DeleteFromAEL(horzEdge);
    }

    private void ProcessHorizontals()
    {
        for (var i = m_SortedEdges; i != null; i = m_SortedEdges)
        {
            DeleteFromSEL(i);
            ProcessHorizontal(i);
        }
    }

    private bool ProcessIntersections(long botY, long topY)
    {
        bool flag;
        if (m_ActiveEdges == null)
        {
            return true;
        }

        try
        {
            BuildIntersectList(botY, topY);
            if (m_IntersectNodes == null)
            {
                flag = true;
            }
            else if (!FixupIntersections())
            {
                flag = false;
            }
            else
            {
                ProcessIntersectList();
                return true;
            }
        }
        catch
        {
            m_SortedEdges = null;
            DisposeIntersectNodes();
            throw new ClipperException("ProcessIntersections error");
        }

        return flag;
    }

    private void ProcessIntersectList()
    {
        while (m_IntersectNodes != null)
        {
            var mIntersectNodes = m_IntersectNodes.next;
            IntersectEdges(m_IntersectNodes.edge1, m_IntersectNodes.edge2, m_IntersectNodes.pt, Protects.ipBoth);
            SwapPositionsInAEL(m_IntersectNodes.edge1, m_IntersectNodes.edge2);
            m_IntersectNodes = null;
            m_IntersectNodes = mIntersectNodes;
        }
    }

    private bool ProcessParam1BeforeParam2(IntersectNode node1, IntersectNode node2)
    {
        bool x;
        if (node1.pt.Y != node2.pt.Y)
        {
            return node1.pt.Y > node2.pt.Y;
        }

        if (node1.edge1 == node2.edge1 || node1.edge2 == node2.edge1)
        {
            x = node2.pt.X > node1.pt.X;
            if (node2.edge1.dx <= 0)
            {
                return x;
            }

            return !x;
        }

        if (node1.edge1 != node2.edge2 && node1.edge2 != node2.edge2)
        {
            return node2.pt.X > node1.pt.X;
        }

        x = node2.pt.X > node1.pt.X;
        if (node2.edge2.dx <= 0)
        {
            return x;
        }

        return !x;
    }

    internal bool Pt3IsBetweenPt1AndPt2(Coordinate pt1, Coordinate pt2, Coordinate pt3)
    {
        if (ClipperBase.PointsEqual(pt1, pt3) || ClipperBase.PointsEqual(pt2, pt3))
        {
            return true;
        }

        if (pt1.X != pt2.X)
        {
            return pt1.X < pt3.X == pt3.X < pt2.X;
        }

        return pt1.Y < pt3.Y == pt3.Y < pt2.Y;
    }

    protected override void Reset()
    {
        base.Reset();
        m_Scanbeam = null;
        m_ActiveEdges = null;
        m_SortedEdges = null;
        DisposeAllPolyPts();
        for (var i = m_MinimaList; i != null; i = i.next)
        {
            InsertScanbeam(i.Y);
            InsertScanbeam(i.leftBound.ytop);
        }
    }

    public static void ReversePolygons(List<List<Coordinate>> polys)
    {
        polys.ForEach((List<Coordinate> poly) => poly.Reverse());
    }

    private void ReversePolyPtLinks(OutPt pp)
    {
        if (pp == null)
        {
            return;
        }

        var outPt = pp;
        do
        {
            var outPt1 = outPt.next;
            outPt.next = outPt.prev;
            outPt.prev = outPt1;
            outPt = outPt1;
        } while (outPt != pp);
    }

    private static long Round(double value)
    {
        if (value >= 0)
        {
            return (long)(value + 0.5);
        }

        return (long)(value - 0.5);
    }

    private void SetHoleState(TEdge e, OutRec outRec)
    {
        var flag = false;
        for (var i = e.prevInAEL; i != null; i = i.prevInAEL)
        {
            if (i.outIdx >= 0)
            {
                flag = !flag;
                if (outRec.FirstLeft == null)
                {
                    outRec.FirstLeft = mPolyOuts[i.outIdx];
                }
            }
        }

        if (flag)
        {
            outRec.isHole = true;
        }
    }

    private void SetWindingCount(TEdge edge)
    {
        var mActiveEdges = edge.prevInAEL;
        while (mActiveEdges != null && mActiveEdges.polyType != edge.polyType)
        {
            mActiveEdges = mActiveEdges.prevInAEL;
        }

        if (mActiveEdges == null)
        {
            edge.windCnt = edge.windDelta;
            edge.windCnt2 = 0;
            mActiveEdges = m_ActiveEdges;
        }
        else if (!IsEvenOddFillType(edge))
        {
            if (mActiveEdges.windCnt * mActiveEdges.windDelta < 0)
            {
                if (Math.Abs(mActiveEdges.windCnt) <= 1)
                {
                    edge.windCnt = mActiveEdges.windCnt + mActiveEdges.windDelta + edge.windDelta;
                }
                else if (mActiveEdges.windDelta * edge.windDelta >= 0)
                {
                    edge.windCnt = mActiveEdges.windCnt + edge.windDelta;
                }
                else
                {
                    edge.windCnt = mActiveEdges.windCnt;
                }
            }
            else if (Math.Abs(mActiveEdges.windCnt) > 1 && mActiveEdges.windDelta * edge.windDelta < 0)
            {
                edge.windCnt = mActiveEdges.windCnt;
            }
            else if (mActiveEdges.windCnt + edge.windDelta != 0)
            {
                edge.windCnt = mActiveEdges.windCnt + edge.windDelta;
            }
            else
            {
                edge.windCnt = mActiveEdges.windCnt;
            }

            edge.windCnt2 = mActiveEdges.windCnt2;
            mActiveEdges = mActiveEdges.nextInAEL;
        }
        else
        {
            edge.windCnt = 1;
            edge.windCnt2 = mActiveEdges.windCnt2;
            mActiveEdges = mActiveEdges.nextInAEL;
        }

        if (!IsEvenOddAltFillType(edge))
        {
            while (mActiveEdges != edge)
            {
                edge.windCnt2 += mActiveEdges.windDelta;
                mActiveEdges = mActiveEdges.nextInAEL;
            }

            return;
        }

        while (mActiveEdges != edge)
        {
            edge.windCnt2 = (edge.windCnt2 == 0 ? 1 : 0);
            mActiveEdges = mActiveEdges.nextInAEL;
        }
    }

    public static List<List<Coordinate>> SimplifyPolygon(List<Coordinate> poly, PolyFillType fillType = 0)
    {
        var lists = new List<List<Coordinate>>();
        var clipper = new Clipper();
        clipper.AddPolygon(poly, PolyType.ptSubject);
        clipper.Execute(ClipType.ctUnion, lists, fillType, fillType);
        return lists;
    }

    public static List<List<Coordinate>> SimplifyPolygons(List<List<Coordinate>> polys, PolyFillType fillType = 0)
    {
        var lists = new List<List<Coordinate>>();
        var clipper = new Clipper();
        clipper.AddPolygons(polys, PolyType.ptSubject);
        clipper.Execute(ClipType.ctUnion, lists, fillType, fillType);
        return lists;
    }

    private void SwapIntersectNodes(IntersectNode int1, IntersectNode int2)
    {
        var tEdge = int1.edge1;
        var tEdge1 = int1.edge2;
        var intPoint = int1.pt;
        int1.edge1 = int2.edge1;
        int1.edge2 = int2.edge2;
        int1.pt = int2.pt;
        int2.edge1 = tEdge;
        int2.edge2 = tEdge1;
        int2.pt = intPoint;
    }

    internal void SwapPoints(ref Coordinate pt1, ref Coordinate pt2)
    {
        var intPoint = pt1;
        pt1 = pt2;
        pt2 = intPoint;
    }

    private static void SwapPolyIndexes(TEdge edge1, TEdge edge2)
    {
        var num = edge1.outIdx;
        edge1.outIdx = edge2.outIdx;
        edge2.outIdx = num;
    }

    private void SwapPositionsInAEL(TEdge edge1, TEdge edge2)
    {
        if (edge1.nextInAEL == edge2)
        {
            var tEdge = edge2.nextInAEL;
            if (tEdge != null)
            {
                tEdge.prevInAEL = edge1;
            }

            var tEdge1 = edge1.prevInAEL;
            if (tEdge1 != null)
            {
                tEdge1.nextInAEL = edge2;
            }

            edge2.prevInAEL = tEdge1;
            edge2.nextInAEL = edge1;
            edge1.prevInAEL = edge2;
            edge1.nextInAEL = tEdge;
        }
        else if (edge2.nextInAEL != edge1)
        {
            var tEdge2 = edge1.nextInAEL;
            var tEdge3 = edge1.prevInAEL;
            edge1.nextInAEL = edge2.nextInAEL;
            if (edge1.nextInAEL != null)
            {
                edge1.nextInAEL.prevInAEL = edge1;
            }

            edge1.prevInAEL = edge2.prevInAEL;
            if (edge1.prevInAEL != null)
            {
                edge1.prevInAEL.nextInAEL = edge1;
            }

            edge2.nextInAEL = tEdge2;
            if (edge2.nextInAEL != null)
            {
                edge2.nextInAEL.prevInAEL = edge2;
            }

            edge2.prevInAEL = tEdge3;
            if (edge2.prevInAEL != null)
            {
                edge2.prevInAEL.nextInAEL = edge2;
            }
        }
        else
        {
            var tEdge4 = edge1.nextInAEL;
            if (tEdge4 != null)
            {
                tEdge4.prevInAEL = edge2;
            }

            var tEdge5 = edge2.prevInAEL;
            if (tEdge5 != null)
            {
                tEdge5.nextInAEL = edge1;
            }

            edge1.prevInAEL = tEdge5;
            edge1.nextInAEL = edge2;
            edge2.prevInAEL = edge1;
            edge2.nextInAEL = tEdge4;
        }

        if (edge1.prevInAEL == null)
        {
            m_ActiveEdges = edge1;
            return;
        }

        if (edge2.prevInAEL == null)
        {
            m_ActiveEdges = edge2;
        }
    }

    private void SwapPositionsInSEL(TEdge edge1, TEdge edge2)
    {
        if (edge1.nextInSEL == null && edge1.prevInSEL == null)
        {
            return;
        }

        if (edge2.nextInSEL == null && edge2.prevInSEL == null)
        {
            return;
        }

        if (edge1.nextInSEL == edge2)
        {
            var tEdge = edge2.nextInSEL;
            if (tEdge != null)
            {
                tEdge.prevInSEL = edge1;
            }

            var tEdge1 = edge1.prevInSEL;
            if (tEdge1 != null)
            {
                tEdge1.nextInSEL = edge2;
            }

            edge2.prevInSEL = tEdge1;
            edge2.nextInSEL = edge1;
            edge1.prevInSEL = edge2;
            edge1.nextInSEL = tEdge;
        }
        else if (edge2.nextInSEL != edge1)
        {
            var tEdge2 = edge1.nextInSEL;
            var tEdge3 = edge1.prevInSEL;
            edge1.nextInSEL = edge2.nextInSEL;
            if (edge1.nextInSEL != null)
            {
                edge1.nextInSEL.prevInSEL = edge1;
            }

            edge1.prevInSEL = edge2.prevInSEL;
            if (edge1.prevInSEL != null)
            {
                edge1.prevInSEL.nextInSEL = edge1;
            }

            edge2.nextInSEL = tEdge2;
            if (edge2.nextInSEL != null)
            {
                edge2.nextInSEL.prevInSEL = edge2;
            }

            edge2.prevInSEL = tEdge3;
            if (edge2.prevInSEL != null)
            {
                edge2.prevInSEL.nextInSEL = edge2;
            }
        }
        else
        {
            var tEdge4 = edge1.nextInSEL;
            if (tEdge4 != null)
            {
                tEdge4.prevInSEL = edge2;
            }

            var tEdge5 = edge2.prevInSEL;
            if (tEdge5 != null)
            {
                tEdge5.nextInSEL = edge1;
            }

            edge1.prevInSEL = tEdge5;
            edge1.nextInSEL = edge2;
            edge2.prevInSEL = edge1;
            edge2.nextInSEL = tEdge4;
        }

        if (edge1.prevInSEL == null)
        {
            m_SortedEdges = edge1;
            return;
        }

        if (edge2.prevInSEL == null)
        {
            m_SortedEdges = edge2;
        }
    }

    private static void SwapSides(TEdge edge1, TEdge edge2)
    {
        var edgeSide = edge1.side;
        edge1.side = edge2.side;
        edge2.side = edgeSide;
    }

    private static long TopX(TEdge edge, long currentY)
    {
        if (currentY == edge.ytop)
        {
            return edge.xtop;
        }

        return edge.xbot + Round(edge.dx * (currentY - edge.ybot));
    }

    private void UpdateEdgeIntoAEL(ref TEdge e)
    {
        if (e.nextInLML == null)
        {
            throw new ClipperException("UpdateEdgeIntoAEL: invalid call");
        }

        var tEdge = e.prevInAEL;
        var tEdge1 = e.nextInAEL;
        e.nextInLML.outIdx = e.outIdx;
        if (tEdge == null)
        {
            m_ActiveEdges = e.nextInLML;
        }
        else
        {
            tEdge.nextInAEL = e.nextInLML;
        }

        if (tEdge1 != null)
        {
            tEdge1.prevInAEL = e.nextInLML;
        }

        e.nextInLML.side = e.side;
        e.nextInLML.windDelta = e.windDelta;
        e.nextInLML.windCnt = e.windCnt;
        e.nextInLML.windCnt2 = e.windCnt2;
        e = e.nextInLML;
        e.prevInAEL = tEdge;
        e.nextInAEL = tEdge1;
        if (e.dx != -3.4E+38)
        {
            InsertScanbeam(e.ytop);
        }
    }

    internal class DoublePoint
    {
        public double X
        {
            get;
            set;
        }

        public double Y
        {
            get;
            set;
        }

        public DoublePoint(double x = 0, double y = 0)
        {
            X = x;
            Y = y;
        }
    }

    private class PolyOffsetBuilder
    {
        private const int buffLength = 128;

        private List<List<Coordinate>> pts;

        private List<Coordinate> currentPoly;

        private List<DoublePoint> normals;

        private double delta;

        private double m_R;

        private int m_i;

        private int m_j;

        private int m_k;

        public PolyOffsetBuilder(List<List<Coordinate>> pts, List<List<Coordinate>> solution, double delta, JoinType jointype, double MiterLimit = 2, bool AutoFix = true)
        {
            if (delta == 0)
            {
                solution = pts;
                return;
            }

            this.pts = pts;
            this.delta = delta;
            if (AutoFix)
            {
                var count = pts.Count;
                var num = 0;
                while (num < count && pts[num].Count == 0)
                {
                    num++;
                }

                if (num == count)
                {
                    return;
                }

                var item = pts[num][0];
                for (var i = num; i < count; i++)
                {
                    if (pts[i].Count != 0)
                    {
                        if (UpdateBotPt(pts[i][0], ref item))
                        {
                            num = i;
                        }

                        for (var j = pts[i].Count - 1; j > 0; j--)
                        {
                            if (ClipperBase.PointsEqual(pts[i][j], pts[i][j - 1]))
                            {
                                pts[i].RemoveAt(j);
                            }
                            else if (UpdateBotPt(pts[i][j], ref item))
                            {
                                num = i;
                            }
                        }
                    }
                }

                if (!Orientation(pts[num]))
                {
                    ReversePolygons(pts);
                }
            }

            if (MiterLimit <= 1)
            {
                MiterLimit = 1;
            }

            var miterLimit = 2 / (MiterLimit * MiterLimit);
            normals = new List<DoublePoint>();
            solution.Clear();
            solution.Capacity = pts.Count;
            m_i = 0;
            while (m_i < pts.Count)
            {
                var count1 = pts[m_i].Count;
                if (count1 > 1 && pts[m_i][0].X == pts[m_i][count1 - 1].X && pts[m_i][0].Y == pts[m_i][count1 - 1].Y)
                {
                    count1--;
                }

                if (count1 != 0 && (count1 >= 3 || delta > 0))
                {
                    if (count1 != 1)
                    {
                        normals.Clear();
                        normals.Capacity = count1;
                        for (var k = 0; k < count1 - 1; k++)
                        {
                            normals.Add(GetUnitNormal(pts[m_i][k], pts[m_i][k + 1]));
                        }

                        normals.Add(GetUnitNormal(pts[m_i][count1 - 1], pts[m_i][0]));
                        currentPoly = new List<Coordinate>();
                        m_k = count1 - 1;
                        m_j = 0;
                        while (m_j < count1)
                        {
                            switch (jointype)
                            {
                                case JoinType.jtSquare:
                                {
                                    DoSquare(1);
                                    break;
                                }
                                case JoinType.jtRound:
                                {
                                    DoRound();
                                    break;
                                }
                                case JoinType.jtMiter:
                                {
                                    m_R = 1 + (normals[m_j].X * normals[m_k].X + normals[m_j].Y * normals[m_k].Y);
                                    if (m_R < miterLimit)
                                    {
                                        DoSquare(MiterLimit);
                                        break;
                                    }
                                    else
                                    {
                                        DoMiter();
                                        break;
                                    }
                                }
                            }

                            m_k = m_j;
                            m_j++;
                        }

                        solution.Add(currentPoly);
                    }
                    else
                    {
                        var intPoints = BuildArc(pts[m_i][count1 - 1], 0, 6.28318530717959, delta);
                        solution.Add(intPoints);
                    }
                }

                m_i++;
            }

            var clipper = new Clipper();
            clipper.AddPolygons(solution, PolyType.ptSubject);
            if (delta > 0)
            {
                clipper.Execute(ClipType.ctUnion, solution, PolyFillType.pftPositive, PolyFillType.pftPositive);
                return;
            }

            var bounds = clipper.GetBounds();
            var intPoints1 = new List<Coordinate>(4)
            {
                new Coordinate {X = bounds.left - 10, Y = bounds.bottom + 10},
                new Coordinate {X = bounds.right + 10, Y = bounds.bottom + 10},
                new Coordinate {X = bounds.right + 10, Y = bounds.top - 10},
                new Coordinate {X = bounds.left - 10, Y = bounds.top - 10}
            };
            clipper.AddPolygon(intPoints1, PolyType.ptSubject);
            clipper.Execute(ClipType.ctUnion, solution, PolyFillType.pftNegative, PolyFillType.pftNegative);
            if (solution.Count > 0)
            {
                solution.RemoveAt(0);
                for (var l = 0; l < solution.Count; l++)
                {
                    solution[l].Reverse();
                }
            }
        }

        internal void AddPoint(Coordinate pt)
        {
            var count = currentPoly.Count;
            if (count == currentPoly.Capacity)
            {
                currentPoly.Capacity = count + 128;
            }

            currentPoly.Add(pt);
        }

        internal void DoMiter()
        {
            if ((normals[m_k].X * normals[m_j].Y - normals[m_j].X * normals[m_k].Y) * delta >= 0)
            {
                var mR = delta / m_R;
                AddPoint(
                    new Coordinate
                    {
                        X = Round(pts[m_i][m_j].X + (normals[m_k].X + normals[m_j].X) * mR),
                        Y = Round(pts[m_i][m_j].Y + (normals[m_k].Y + normals[m_j].Y) * mR)
                    });
                return;
            }

            var intPoint = new Coordinate
            {
                X = Round(pts[m_i][m_j].X + normals[m_k].X * delta),
                Y = Round(pts[m_i][m_j].Y + normals[m_k].Y * delta)
            };
            var intPoint1 = new Coordinate
            {
                X = Round(pts[m_i][m_j].X + normals[m_j].X * delta),
                Y = Round(pts[m_i][m_j].Y + normals[m_j].Y * delta)
            };
            AddPoint(intPoint);
            AddPoint(pts[m_i][m_j]);
            AddPoint(intPoint1);
        }

        internal void DoRound()
        {
            var intPoint = new Coordinate
            {
                X = Round(pts[m_i][m_j].X + normals[m_k].X * delta),
                Y = Round(pts[m_i][m_j].Y + normals[m_k].Y * delta)
            };
            var intPoint1 = new Coordinate
            {
                X = Round(pts[m_i][m_j].X + normals[m_j].X * delta),
                Y = Round(pts[m_i][m_j].Y + normals[m_j].Y * delta)
            };
            AddPoint(intPoint);
            if ((normals[m_k].X * normals[m_j].Y - normals[m_j].X * normals[m_k].Y) * delta < 0)
            {
                AddPoint(pts[m_i][m_j]);
            }
            else if (normals[m_j].X * normals[m_k].X + normals[m_j].Y * normals[m_k].Y < 0.985)
            {
                var num = Math.Atan2(normals[m_k].Y, normals[m_k].X);
                var num1 = Math.Atan2(normals[m_j].Y, normals[m_j].X);
                if (delta > 0 && num1 < num)
                {
                    num1 += 6.28318530717959;
                }
                else if (delta < 0 && num1 > num)
                {
                    num1 -= 6.28318530717959;
                }

                var intPoints = BuildArc(pts[m_i][m_j], num, num1, delta);
                for (var i = 0; i < intPoints.Count; i++)
                {
                    AddPoint(intPoints[i]);
                }
            }

            AddPoint(intPoint1);
        }

        internal void DoSquare(double mul)
        {
            var intPoint = new Coordinate
            {
                X = Round(pts[m_i][m_j].X + normals[m_k].X * delta),
                Y = Round(pts[m_i][m_j].Y + normals[m_k].Y * delta)
            };
            var intPoint1 = new Coordinate
            {
                X = Round(pts[m_i][m_j].X + normals[m_j].X * delta),
                Y = Round(pts[m_i][m_j].Y + normals[m_j].Y * delta)
            };
            if ((normals[m_k].X * normals[m_j].Y - normals[m_j].X * normals[m_k].Y) * delta < 0)
            {
                AddPoint(intPoint);
                AddPoint(pts[m_i][m_j]);
                AddPoint(intPoint1);
                return;
            }

            var num = Math.Atan2(normals[m_k].Y, normals[m_k].X);
            var num1 = Math.Atan2(-normals[m_j].Y, -normals[m_j].X);
            num = Math.Abs(num1 - num);
            if (num > 3.14159265358979)
            {
                num = 6.28318530717959 - num;
            }

            var num2 = Math.Tan((3.14159265358979 - num) / 4) * Math.Abs(delta * mul);
            intPoint = new Coordinate
            {
                X = (long)(intPoint.X - normals[m_k].Y * num2),
                Y = (long)(intPoint.Y + normals[m_k].X * num2)
            };
            AddPoint(intPoint);
            intPoint1 = new Coordinate
            {
                X = (long)(intPoint1.X + normals[m_j].Y * num2),
                Y = (long)(intPoint1.Y - normals[m_j].X * num2)
            };

            AddPoint(intPoint1);
        }

        internal bool UpdateBotPt(Coordinate pt, ref Coordinate botPt)
        {
            if (pt.Y <= botPt.Y && (pt.Y != botPt.Y || pt.X >= botPt.X))
            {
                return false;
            }

            botPt = pt;
            return true;
        }
    }
}