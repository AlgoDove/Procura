# Vendor Management: Frontend Completion Walkthrough (Web & Mobile)

**Date:** 2026-09-27  
**Branch:** `feature/vendor-management`  
**Commits:**
- `f3826d3` — `fix(vendor-web): align vendor types and pages with backend DTO contract`
- `04ce5c6` — `feat(vendor-mobile): add vendor list, detail, create, and edit screens`

---

## 1. Executive Summary & Objective

Procura is a university group assignment (SE3090 — Software Engineering Frameworks) implementing a full-stack procurement management system with an agentic AI subsystem. It operates as a modular monolith comprising:
- **Backend:** ASP.NET Core 8 Web API (EF Core, PostgreSQL, JWT Authentication)
- **Frontend-Web:** React (Vite, TypeScript, TanStack Query, CSS Modules)
- **Frontend-Mobile:** Flutter (Dart, Provider, Dio, Material 3)

This work focused exclusively on **Vendor Management** frontend completion:
1. **Frontend-Web Audit & Alignment:** Audited and aligned existing React vendor management pages and TypeScript definitions with the backend API contract.
2. **Frontend-Mobile Implementation from Scratch:** Flutter currently had zero vendor screens. Built the complete Vendor Management mobile UI (list, detail, create, edit) following the project's established conventions, registered routes, added an entry point, and created automated widget tests.
3. **Verification & Push:** Validated both codebases with automated test, lint, and build suites before pushing cleanly to the remote branch.

---

## 2. Real Backend API Contract Reference

Derived directly from `backend/Procura.API/Modules/VendorManagement/DTOs/VendorDtos.cs` and `VendorsController.cs`:

### DTO Schema

| JSON Field | Type | Create / Update | Response | Notes |
|---|---|---|---|---|
| `id` | `string` (UUID) | No | Yes | Vendor unique identifier |
| `name` | `string` | **Required** | Yes | Max 150 chars |
| `contactPerson` | `string` | **Required** | Yes | Max 100 chars |
| `email` | `string` | **Required** | Yes | Valid email address, Max 150 chars |
| `phoneNumber` | `string` | **Required** | Yes | Max 30 chars |
| `address` | `string?` | Optional | Yes | Optional street address |
| `category` | `string` | **Required** | Yes | Max 60 chars |
| `rating` | `number` | Optional (default: 0) | Yes | Decimal between 0.0 and 5.0 |
| `status` | `string` | No | Yes | `"ACTIVE"` or `"INACTIVE"` |
| `createdAt` | `string` (ISO 8601) | No | Yes | Server timestamp |
| `updatedAt` | `string` (ISO 8601) | No | Yes | Server timestamp |

> **Note on fields removed/disallowed:** Legacy references to `contactEmail`, `contactPhone`, or an unsupported `notes` field are not part of the backend contract.

### Endpoints & Role Authorization

All endpoints require JWT Bearer Authentication (`[Authorize]`):

- `GET /api/vendors` — List all vendors (supports optional `?status=` and `?category=`). **Any authenticated role**.
- `GET /api/vendors/{id}` — Get single vendor detail. **Any authenticated role**.
- `POST /api/vendors` — Create vendor. Roles: `PROCUREMENT_OFFICER`, `MANAGER`, `ADMIN`.
- `PUT /api/vendors/{id}` — Update vendor. Roles: `PROCUREMENT_OFFICER`, `MANAGER`, `ADMIN`.
- `POST /api/vendors/{id}/deactivate` — Deactivate vendor (sets status to `INACTIVE`, returns 409 if already inactive). Roles: `PROCUREMENT_OFFICER`, `MANAGER`, `ADMIN`.
- `POST /api/vendors/{id}/activate` — Activate vendor (sets status to `ACTIVE`, returns 409 if already active). Roles: `PROCUREMENT_OFFICER`, `MANAGER`, `ADMIN`.

---

## 3. Stage 1: Frontend-Web Verification & Fixes

### 3.1 Audit Findings
- **`frontend-web/src/types/api.ts`**:
  - `VendorResponse`, `CreateVendorDto`, and `UpdateVendorDto` were already updated to use `contactPerson`, `email`, `phoneNumber`, `address`, `category`, and `rating: number` with no `notes` field.
- **`frontend-web/src/pages/CreateVendorPage.tsx`**:
  - Inputs bound to `name`, `category`, `contactPerson`, `email`, `phoneNumber`, `rating`, `address`. No `contactEmail`, `contactPhone`, or `notes`.
- **`frontend-web/src/pages/EditVendorPage.tsx`**:
  - Pre-fills and mutates all valid fields matching the backend contract.
- **`frontend-web/src/pages/VendorDetailPage.tsx`**:
  - Rendered `vendor.contactPerson`, `vendor.email`, `vendor.phoneNumber`, `vendor.address`, `vendor.rating`, `vendor.createdAt`, and `vendor.updatedAt`.
  - **Identified Gap:** Role authorization check `canManage` only checked `PROCUREMENT_OFFICER` and `ADMIN`, missing `MANAGER`.
- **`frontend-web/src/pages/VendorsListPage.tsx`**:
  - Table rendered `v.email`, `v.rating.toFixed(1)`, and status badge.
  - **Identified Gap:** `canManage` role check was also missing `MANAGER`.

### 3.2 Changes Made
1. **`frontend-web/src/pages/VendorDetailPage.tsx`**:
   - Updated `canManage` role gating to include `MANAGER`:
     ```ts
     const canManage =
       user?.role === 'PROCUREMENT_OFFICER' ||
       user?.role === 'MANAGER' ||
       user?.role === 'ADMIN';
     ```
2. **`frontend-web/src/pages/VendorsListPage.tsx`**:
   - Updated `canManage` role gating to include `MANAGER`:
     ```ts
     const canManage =
       user?.role === 'PROCUREMENT_OFFICER' ||
       user?.role === 'MANAGER' ||
       user?.role === 'ADMIN';
     ```
3. **`frontend-web/src/test/setup.ts`**:
   - Preserved fallback for `import.meta.env.VITE_API_BASE_URL` so Vitest suites run smoothly without missing environment configurations.

### 3.3 Verification Results
- **`npm run build`**: Passed cleanly (`tsc -b && vite build` built production dist in 295ms).
- **`npm run test`**: Passed (13/13 unit tests passed across 3 test files).
- **`npm run lint`**: Passed (0 errors; 3 standard fast refresh/compiler warnings).

---

## 4. Stage 2: Flutter Vendor Management Implementation (from Scratch)

### 4.1 Architectural Conventions Followed
- **State Management:** `Provider` for authentication/role state; `StatefulWidget` + `setState` for per-screen UI state (ADR-003).
- **HTTP Client:** Reused singleton `ApiClient.dio` with JWT interceptors.
- **Design System:** Material 3 utilizing existing `ThemeData` (`seedColor: 0xFF1E3A5F`).
- **Dates & Formatting:** Standardized date display using parsed UTC strings formatted for local display.

### 4.2 Files Created & Modified

#### A. Models: `frontend-mobile/lib/models/api_models.dart` (Modified)
Added vendor model classes at the end of the file without touching existing procurement models:
- `VendorResponse`: Factory `VendorResponse.fromJson(Map<String, dynamic> j)` with safe num-to-double casting for `rating`.
- `CreateVendorDto`: Implemented with `toJson()` payload matching API contract.
- `UpdateVendorDto`: Implemented with `toJson()` payload matching API contract.

#### B. Vendor List Screen: `frontend-mobile/lib/screens/vendors_list_screen.dart` (Created)
- **Lifecycle & Fetching:** Calls `GET /api/vendors` in `initState`.
- **Search & Filters:**
  - Real-time search query matching vendor name, contact person, or category.
  - Status filter popup menu (`All`, `ACTIVE`, `INACTIVE`) with active filter chips.
  - Category filter popup menu dynamically generated from loaded vendor data.
- **Card Presentation:** Custom Material 3 cards showing vendor name, category pill, status badge (green for `ACTIVE`, grey for `INACTIVE`), star rating (`★ 4.5`), contact person, and email.
- **Role-Gating:** Floating Action Button (`+ New Vendor`) conditionally rendered for `PROCUREMENT_OFFICER`, `MANAGER`, and `ADMIN`.
- **Resilience:** Pull-to-refresh (`RefreshIndicator`), error state with retry button, and empty state with action link.

#### C. Vendor Detail Screen: `frontend-mobile/lib/screens/vendor_detail_screen.dart` (Created)
- **Constructor:** Accepts `required String vendorId`.
- **Data Fetching:** Calls `GET /api/vendors/{id}`.
- **Header & Info:** Renders vendor title, category, status pill, formatted creation & update dates, and contact summary card.
- **Actions (Role-gated):**
  - **Edit:** Navigates to `EditVendorScreen` and automatically refreshes upon returning.
  - **Deactivate:** Enabled for active vendors, sends `POST /api/vendors/{id}/deactivate` with 409 handling.
  - **Activate:** Enabled for inactive vendors, sends `POST /api/vendors/{id}/activate`.

#### D. Create Vendor Screen: `frontend-mobile/lib/screens/create_vendor_screen.dart` (Created)
- **Form Controls:** Form with `GlobalKey<FormState>()` and text controllers for `name`, `category`, `contactPerson`, `email`, `phoneNumber`, `rating`, and `address`.
- **Validation:**
  - Mandatory checks for name, category, contact person, email, and phone.
  - Regex pattern validation for email addresses (`^[^@]+@[^@]+\.[^@]+`).
  - Range validation for rating (`0.0 <= rating <= 5.0`).
- **Submission:** Submits `POST /api/vendors` with loading indicator. Extracts API `ProblemDetails` or field validation errors on failure.
- **Navigation:** Replaces route with `VendorDetailScreen(vendorId: created.id)` on success.

#### E. Edit Vendor Screen: `frontend-mobile/lib/screens/edit_vendor_screen.dart` (Created)
- **Data Hydration:** Fetches existing vendor record (`GET /api/vendors/{id}`) and pre-populates form controllers.
- **Update Handling:** Submits `PUT /api/vendors/{id}` and pops navigation stack returning `true` to signal parent screen refresh.

#### F. Navigation & Routing Updates
1. **`frontend-mobile/lib/main.dart` (Modified):**
   - Registered `/vendors` and `/vendors/create` routes in `onGenerateRoute`.
   - Handled dynamic routes `/vendors/{id}` and `/vendors/{id}/edit` additively within the default routing switch.
2. **`frontend-mobile/lib/screens/requests_list_screen.dart` (Modified):**
   - Added a minimal 5-line entry point icon button in `AppBar.actions`:
     ```dart
     IconButton(
       icon: const Icon(Icons.store),
       tooltip: 'Vendors',
       onPressed: () => Navigator.pushNamed(context, '/vendors'),
     ),
     ```

#### G. Automated Widget Tests: `frontend-mobile/test/vendor_screens_test.dart` (Created)
- Verified all form fields and action buttons render correctly in `CreateVendorScreen`.
- Tested empty form submission validation triggers appropriate error text for required fields.
- Tested email format validation on invalid input.

### 4.3 Verification Results
- **`flutter pub get`**: All packages resolved.
- **`flutter analyze`**: **`No issues found!`** (0 warnings, 0 errors).
- **`flutter test`**: **9/9 tests passed** across all test suites.

---

## 5. Summary of Files Changed & Created

| File | Status | Description |
|---|---|---|
| `frontend-web/src/pages/VendorDetailPage.tsx` | Modified | Updated `canManage` to include `MANAGER` role. |
| `frontend-web/src/pages/VendorsListPage.tsx` | Modified | Updated `canManage` to include `MANAGER` role. |
| `frontend-web/src/test/setup.ts` | Modified | Added default fallback for `VITE_API_BASE_URL`. |
| `frontend-mobile/lib/models/api_models.dart` | Modified | Added `VendorResponse`, `CreateVendorDto`, `UpdateVendorDto`. |
| `frontend-mobile/lib/main.dart` | Modified | Added vendor screen imports and routes in `onGenerateRoute`. |
| `frontend-mobile/lib/screens/requests_list_screen.dart` | Modified | Added minimal `Icons.store` button in AppBar actions. |
| `frontend-mobile/lib/screens/vendors_list_screen.dart` | **Created** | Full vendor listing screen with search, filters, cards, and role gating. |
| `frontend-mobile/lib/screens/vendor_detail_screen.dart` | **Created** | Vendor detail screen with Activate/Deactivate/Edit actions. |
| `frontend-mobile/lib/screens/create_vendor_screen.dart` | **Created** | Form screen to create vendors with validation and API error parsing. |
| `frontend-mobile/lib/screens/edit_vendor_screen.dart` | **Created** | Form screen to edit existing vendor details. |
| `frontend-mobile/test/vendor_screens_test.dart` | **Created** | Widget test suite for vendor screens. |
| `docs/walkthroughs/VENDOR_MANAGEMENT_FRONTEND_COMPLETION.md` | **Created** | Comprehensive technical and architectural summary document. |

---

## 6. Manual Click-Through Testing Protocol

### Web Manual Verification
1. Open web application: `http://localhost:5173/login`.
2. Log in as a user with role `PROCUREMENT_OFFICER`, `MANAGER`, or `ADMIN`.
3. Click **Vendors** in the top navigation bar.
4. Verify table columns: `Name`, `Category`, `Contact Email`, `Status`, `Rating`, and `Actions`.
5. Click **+ New Vendor**, fill in valid details, and submit. Confirm redirect to the detail page.
6. On the detail page, verify all fields. Click **Edit**, change a value (e.g. Rating), and save.
7. Test the **Deactivate** button to confirm status toggles to `INACTIVE`.
8. Test the **Activate** button to confirm status toggles back to `ACTIVE`.

### Mobile Manual Verification (Android Emulator / iOS Simulator)
1. Run application: `flutter run -d emulator-5554` (or target device).
2. Log in as a `PROCUREMENT_OFFICER`, `MANAGER`, or `ADMIN` user.
3. In `RequestsListScreen`, tap the store icon (`Icons.store`) in the top right AppBar.
4. In `VendorsListScreen`:
   - Search vendors by typing into the search box.
   - Filter by status using the filter menu icon.
   - Filter by category using the category menu icon.
   - Tap **+ New Vendor** FAB.
5. In `CreateVendorScreen`:
   - Attempt submitting empty form to verify client-side validation errors.
   - Fill in all fields with valid information and tap **Create Vendor**.
6. In `VendorDetailScreen`:
   - Verify all details match the input.
   - Tap **Edit**, modify rating or phone, and tap **Save Changes**.
   - Tap **Deactivate**; verify status changes to `INACTIVE` and button changes to **Activate**.
   - Tap **Activate**; verify status returns to `ACTIVE`.

---

## 7. Git Verification & Push Status

All changes are committed cleanly and pushed to the remote repository:
- **Remote:** `https://github.com/AlgoDove/Procura.git`
- **Branch:** `feature/vendor-management`
- **Latest Commit:** `04ce5c6feat(vendor-mobile): add vendor list, detail, create, and edit screens`
- **Working Tree Status:** `Clean (nothing to commit, working tree clean)`
