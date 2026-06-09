"# Nephila" 
content = """# Force Density Equilibrium — Grasshopper Plugin

**A Grasshopper/Rhino plugin for form-finding of cable-net and shell structures using the Force Density Method (FDM).**

---

## Description

This plugin implements the iterative Force Density Method for structural form-finding in Rhino/Grasshopper. It supports:

- Free and anchored nodes
- Variable force densities per edge
- External load vectors per node
- Fixed edge length constraints (cables, struts)
- Convergence detection with iteration + time display

---

## Installation

1. Build the project in Visual Studio (Release)
2. Copy the resulting `.gha` file to:

%APPDATA%\Grasshopper\Libraries\

3. Unblock the file (Right-click → Properties → Unblock)
4. Restart Rhino

---

## Inputs

| Name | Type | Description |
|------|------|-------------|
| `anchorIndices` | `List<int>` | Indices of fixed anchor points |
| `points` | `List<Point3d>` | Input point positions |
| `edges` | `List<Line>` | Input edges |
| `PP` | `DataTree<int>` | Point–Point adjacency |
| `PL` | `DataTree<int>` | Point–Edge adjacency |
| `P` | `List<Vector3d>` | External loads per node |
| `q` | `List<double>` | Force density per edge |
| `fixedEdges` | `List<int>` | Edge indices with fixed length *(optional)* |
| `targetLengths` | `List<double>` | Target lengths for fixed edges *(optional)* |
| `maxIterations` | `int` | Maximum iterations (capped at 10,000) |

---

## Outputs

| Name | Type | Description |
|------|------|-------------|
| `A` | `List<Point3d>` | Resulting node positions |
| `EdgeLines` | `List<Line>` | Resulting edge geometry |
| `F_out` | `List<double>` | Force per edge `(q · L)` |
| `L_out` | `List<double>` | Length per edge |

---

## Method

The Force Density Method (Schek, 1974) solves the equilibrium of a network by expressing nodal equilibrium as:

$$\\sum_{j \\in N(i)} q_{ij} \\cdot (\\mathbf{x}_j - \\mathbf{x}_i) + \\mathbf{p}_i = 0$$

where $q_{ij}$ is the force density (force/length) of edge $ij$ and $\\mathbf{p}_i$ is the external load at node $i$.

The iterative solver repeats until:

$$\\max_i \\| \\mathbf{x}_i^{(k)} - \\mathbf{x}_i^{(k-1)} \\| < \\varepsilon$$

---

## Requirements

- Rhino 7 or 8
- Grasshopper (included in Rhino)
- .NET Framework 4.8

---

## License

MIT License

Copyright (c) [2026] [Baris Wenzel]

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.


---

## Citation

If you use this software in academic work, please cite:

```bibtex
@software{yourname_fdm_2025,
  author    = {Baris Wenzel},
  title     = {Force Density Equilibrium -- Grasshopper Plugin},
  year      = {2026},
  url       = {https://github.com/barisWenzel/Nephila},
  note      = {Developed as part of doctoral dissertation, Universitaet Stuttgart}
}

References

    Schek, H.-J. (1974). The force density method for form finding and computation of general networks. Computer Methods in Applied Mechanics and Engineering, 3(1), 115–134.

Author

[Baris Wenzel]
[Universitaet Stuttgart]
