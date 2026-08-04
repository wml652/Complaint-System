#!/bin/bash
cd D:\Complaint-System
dotnet ef database update --project src/StudentComplaintPortal.Data --startup-project src/StudentComplaintPortal.Web
