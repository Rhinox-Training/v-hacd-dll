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
    VHACD::Vertex** data)
{
    auto vhacd = (VHACD::IVHACD*)pVHACD;
    VHACD::IVHACD::ConvexHull ch;
    bool found = vhacd->GetConvexHull(index, ch);

    if (found)
    {
        *data = ch.m_points.data();
        return (uint32_t) ch.m_points.size();
    }
    
    return 0;
}

EXTERN uint32_t GetConvexHullTriangles(
    void* pVHACD,
    const uint32_t index,
    VHACD::Triangle** data)
{
    auto vhacd = (VHACD::IVHACD*)pVHACD;
    VHACD::IVHACD::ConvexHull ch;
    bool found = vhacd->GetConvexHull(index, ch);

    if (found)
    {
        *data = ch.m_triangles.data();
        return (uint32_t) ch.m_triangles.size();
    }
    
    return 0;
}