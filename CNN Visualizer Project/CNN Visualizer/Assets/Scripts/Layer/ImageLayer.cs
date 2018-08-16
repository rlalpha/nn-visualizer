﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

public class ImageLayer : Layer, I2DMapLayer
{
    public Vector2Int fullResolution = new Vector2Int(224, 224);
    private Vector2Int _oldFullResolution;

    public Vector2Int reducedResolution = new Vector2Int(11, 11);
    private Vector2Int _oldreducedResolution;

    [Range(0.0f, 1.0f)]
    public float spread = 1.0f;

    [Range(0.0f, 0.1f)]
    public float pixelSpacing = 0.05f;

    [Range(0.0f, 1.0f)]
    public float xToZ = 0.0f;

    [Range(0.1f, 5.0f)]
    public float fullresOffset = 1.0f;

    public float nodeSize;
    public bool showOriginalResolution = false;

    public bool rgb = true;

    private GridShape _fullResGrid;

    private Dictionary<int, Array> _activationTensorPerEpoch = new Dictionary<int, Array>();
    private int[] _activationTensorShape = new int[4];

    public ImageLayer()
    {
        depth = 3;
    }

    protected override void UpdateForChangedParams(bool topoChanged)
    {
        base.UpdateForChangedParams(topoChanged);

        UpdateFeatureMapsForInputParams(topoChanged);


        //TODO: fullres is broken at the moment
        /*if (_fullResGrid == null)
            _fullResGrid = new GridShape(new Vector3(0, 0, this.ZPosition() - fullresOffset), fullResolution, new Vector2(pixelSpacing, pixelSpacing));

        _fullResGrid.position = new Vector3(0, 0, -fullresOffset);
        _fullResGrid.spacing = new Vector2(pixelSpacing, pixelSpacing);
        _fullResGrid.resolution = fullResolution;*/
    }

    protected override Vector3[] GetInterpolatedFeatureMapPositions()
    {
        int reducedDepthGridShape = Mathf.CeilToInt(Mathf.Sqrt(depth));
        Vector3[] xLinePositions = LineShape.ScaledUnitLine(depth, new Vector3(0, 0, 0), new Vector3(1, 0, 0), spread);
        Vector3[] zLinePositions = LineShape.ScaledUnitLine(depth, new Vector3(0, 0, 0), new Vector3(0, 0, 1), spread);

        Vector3[] filterPositions = Shape.InterpolateShapes(xLinePositions, zLinePositions, xToZ);

        return filterPositions;
    }

    public override void CalcMesh()
    {
        _mesh.subMeshCount = 3;

        List<Vector3> verts = new List<Vector3>();
        List<int> inds = new List<int>();
        List<int> fullResInds = new List<int>();
        List<int> linesInds = new List<int>();
        List<Color> cols = new List<Color>();

        AddNodes(verts, inds, fullResInds, linesInds);

        if (!rgb)
        {
            for (int i = 0; i < verts.Count; i++)
            {
                if (_activationTensorPerEpoch.ContainsKey(epoch))
                {

                    int[] index = Util.GetSampleMultiDimIndices(_activationTensorShape, i, GlobalManager.Instance.testSample);

                    Array activationTensor = _activationTensorPerEpoch[epoch];
                    float tensorVal = (float)activationTensor.GetValue(index) * pointBrightness;
                    cols.Add(new Color(tensorVal, tensorVal, tensorVal, 1f));
                }
                else
                {
                    cols.Add(Color.black);
                }
            }
        } else
        {
            AddRgbColors(verts, cols, inds, fullResInds, linesInds);
        }

        _mesh.SetVertices(verts);
        _mesh.SetColors(cols);
        _mesh.SetIndices(inds.ToArray(), MeshTopology.Points, 0);
        _mesh.SetIndices(fullResInds.ToArray(), MeshTopology.Points, 1);
        _mesh.SetIndices(linesInds.ToArray(), MeshTopology.Lines, 2);
    }

    protected void AddNodes(List<Vector3> verts, List<int> inds, List<int> fullResInds, List<int> lineInds)
    {
        base.AddNodes(verts, inds);

        if (showOriginalResolution)
        {
            //TODO: temporarilly disabled
            //AddFullResGeometry(verts, fullResInds, lineInds);
        }
    }

    private void AddFullResGeometry(List<Vector3> verts, List<int> fullResInds, List<int> lineInds)
    {
        Vector3[] v = _fullResGrid.GetVertices(true);

        verts.AddRange(v);
        for (int i = 0; i < v.Length; i++)
        {
            fullResInds.Add(verts.Count - v.Length + i);
        }

        GridShape centerGrid = _featureMaps[_featureMaps.Count / 2].GetPixelGrid();
        float[] bbox = centerGrid.GetBbox();

        float[] zpos = { 0, -fullresOffset };

        List<int> lineStartEndInds = new List<int>();
        foreach (float z in zpos)
        {
            verts.Add(new Vector3(bbox[0], bbox[1], centerGrid.position.z + z));
            lineStartEndInds.Add(verts.Count - 1);

            verts.Add(new Vector3(bbox[0], bbox[3], centerGrid.position.z + z));
            lineStartEndInds.Add(verts.Count - 1);

            verts.Add(new Vector3(bbox[2], bbox[1], centerGrid.position.z + z));
            lineStartEndInds.Add(verts.Count - 1);

            verts.Add(new Vector3(bbox[2], bbox[3], centerGrid.position.z + z));
            lineStartEndInds.Add(verts.Count - 1);
        }
        for (int i = 0; i < 4; i++)
        {
            lineInds.Add(lineStartEndInds[i]);
            lineInds.Add(lineStartEndInds[i + 4]);
        }
    }

    private void AddRgbColors(List<Vector3> verts, List<Color> colors, List<int> inds, List<int> fullResInds, List<int> lineInds)
    {
        if(inds.Count % 3 != 0)
        {
            throw new Exception("Vert count must be dividable by 3 (R G B maps)!");
        }

        int perMapCount = inds.Count / 3;

        for(int i=0; i<verts.Count; i++) {
            if(Mathf.FloorToInt(i/perMapCount) == 0)
            {
                colors.Add(Color.red);
            }
            else if (Mathf.FloorToInt(i / perMapCount) == 1)
            {
                colors.Add(Color.green);
            }
            else if (Mathf.FloorToInt(i / perMapCount) == 2)
            {
                colors.Add(Color.blue);
            } else
            {
                colors.Add(Color.white);
            }
        }
    }

    protected override List<GridShape> GetPointShapes()
    {
        UpdateFeatureMapsForInputParams(true);

        List<GridShape> pixelGrids = new List<GridShape>();
        for (int i = 0; i < _featureMaps.Count; i++)
        {
            pixelGrids.Add(_featureMaps[i].GetPixelGrid());
        }
        return pixelGrids;
    }

    public override Vector3Int GetOutputShape()
    {
        return new Vector3Int(reducedResolution.x, reducedResolution.y, depth);
    }

    public override Vector2Int Get2DOutputShape()
    {
        return reducedResolution;
    }

    public void SetActivationTensorForEpoch(Array tensor, int epoch)
    {
        _activationTensorPerEpoch[epoch] = tensor;
    }

    public Array GetActivationTensorForEpoch(int epoch)
    {
        return _activationTensorPerEpoch[epoch];
    }

    public void SetActivationTensorShape(int[] tensorShape)
    {
        this._activationTensorShape = tensorShape;
    }

    public int[] GetActivationTensorShape()
    {
        return _activationTensorShape;
    }

    public override FeatureMapInputProperties GetFeatureMapInputProperties(int featureMapIndex)
    {
        Vector3[] filterPositions = GetInterpolatedFeatureMapPositions(); //not ideal  recalculating this everytime, but should have minor performance impact
        FeatureMapInputProperties info = new FeatureMapInputProperties();
        info.position = filterPositions[featureMapIndex];
        info.inputShape = reducedResolution;
        info.spacing = pixelSpacing;
        return info;
    }

    protected override bool CheckTopologyChanged_OneValidCall()
    {
        bool topologyChanged = false;

        topologyChanged |= base.CheckTopologyChanged_OneValidCall();
        topologyChanged |= CheckResolutionChanged_OneValidCall();
        topologyChanged |= CheckFullResolutionChanged_OneValidCall();

        return topologyChanged;
    }

    protected bool CheckResolutionChanged_OneValidCall()
    {
        if (reducedResolution != _oldreducedResolution)
        {
            _oldreducedResolution = reducedResolution;
            return true;
        }
        return false;
    }

    protected bool CheckFullResolutionChanged_OneValidCall()
    {
        if (fullResolution != _oldFullResolution)
        {
            _oldFullResolution = fullResolution;
            return true;
        }
        return false;
    }
}
