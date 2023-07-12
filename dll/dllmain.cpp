// dllmain.cpp : Defines the entry point for the DLL application.
#include "pch.h"

BOOL APIENTRY DllMain( HMODULE hModule,
                       DWORD  ul_reason_for_call,
                       LPVOID lpReserved
                     )
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
    case DLL_THREAD_ATTACH:
    case DLL_THREAD_DETACH:
    case DLL_PROCESS_DETACH:
        break;
    }
    return TRUE;
}

#include "..\include\VHACD.h"

#ifdef WIN32
#define EXTERN extern "C" __declspec(dllexport)
#else
#define EXTERN extern "C"
#endif

EXTERN void* CreateVHACD()
{
    return VHACD::CreateVHACD();
}

EXTERN void* CreateVHACD_ASYNC()
{
    return VHACD::CreateVHACD_ASYNC();
}

EXTERN void DestroyVHACD(void* pVHACD)
{
    auto vhacd = (VHACD::IVHACD*)pVHACD;
    vhacd->Clean();
    vhacd->Release();
}

EXTERN bool ComputeFloat(
    void* pVHACD,
    const float* const points,
    const uint32_t countPoints,
    const uint32_t* const triangles,
    const uint32_t countTriangles,
    const void* params)
{
    auto vhacd = (VHACD::IVHACD*)pVHACD;
    return vhacd->Compute(points, countPoints, triangles, countTriangles, *(VHACD::IVHACD::Parameters const*)params);
}

EXTERN bool ComputeDouble(
    void* pVHACD,
    const double* const points,
    const uint32_t countPoints,
    const uint32_t* const triangles,
    const uint32_t countTriangles,
    const void* params)
{
    auto vhacd = (VHACD::IVHACD*)pVHACD;
    return vhacd->Compute(points, countPoints, triangles, countTriangles, *(VHACD::IVHACD::Parameters const*)params);
}

EXTERN void Cancel(void* pVHACD)
{
    auto vhacd = (VHACD::IVHACD*)pVHACD;
    vhacd->Cancel();
}

EXTERN bool IsReady(void* pVHACD)
{
    auto vhacd = (VHACD::IVHACD*)pVHACD;
    return vhacd->IsReady();
}

EXTERN uint32_t findNearestConvexHull(void* pVHACD, const double pos[3], double& distanceToHull)
{
    auto vhacd = (VHACD::IVHACD*)pVHACD;
    return vhacd->findNearestConvexHull(pos, distanceToHull);
}

EXTERN uint32_t GetNConvexHulls(void* pVHACD)
{
    auto vhacd = (VHACD::IVHACD*)pVHACD;
    return vhacd->GetNConvexHulls();
}

EXTERN bool GetConvexHull(
    void* pVHACD,
    const uint32_t index,
    void* ch)
{
    auto vhacd = (VHACD::IVHACD*)pVHACD;
    return vhacd->GetConvexHull(index, *(VHACD::IVHACD::ConvexHull*)ch);
}

EXTERN uint32_t GetConvexHullVertices(
    void* pVHACD,
    const uint32_t index,
    intptr_t* hData,
    double** data)
{
    auto vhacd = (VHACD::IVHACD*)pVHACD;
    VHACD::IVHACD::ConvexHull ch;
    bool found = vhacd->GetConvexHull(index, ch);

    auto vertices = new std::vector<double>();
    for (auto v : ch.m_points)
    {
        vertices->push_back(v.mX);
        vertices->push_back(v.mY);
        vertices->push_back(v.mZ);
    }

    *hData = reinterpret_cast<intptr_t>(vertices);
    *data = vertices->data();
    return ch.m_points.size();
}

EXTERN uint32_t GetConvexHullTriangles(
    void* pVHACD,
    const uint32_t index,
    intptr_t* hData,
    uint32_t** data)
{
    auto vhacd = (VHACD::IVHACD*)pVHACD;
    VHACD::IVHACD::ConvexHull ch;
    bool found = vhacd->GetConvexHull(index, ch);

    auto indices = new std::vector<uint32_t>();
    for (auto i : ch.m_triangles)
    {
        indices->push_back(i.mI0);
        indices->push_back(i.mI1);
        indices->push_back(i.mI2);
    }

    *hData = reinterpret_cast<intptr_t>(indices);
    *data = indices->data();
    return indices->size();
}