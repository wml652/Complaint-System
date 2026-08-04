# IMPLEMENTATION GUIDE: Category Icons & Typing Indicators

---

## **FEATURE 1: CATEGORY ICONS & VISUAL IMPROVEMENTS**

### Changes Required:

#### **1. Database Migration**
**File**: `src/StudentComplaintPortal.Data/Migrations/[NewMigration].cs`
```csharp
// Add columns to Categories table:
// - Icon (nvarchar(50), nullable) - stores emoji or icon code
// - Color (nvarchar(7), nullable) - stores hex color code (#007bff format)
```

**File**: `src/StudentComplaintPortal.Domain/Entities/Category.cs` ✅ DONE
- Added `Icon` property (default: "📋")
- Added `Color` property (default: "#007bff")

#### **2. DTOs Updated**
**File**: `src/StudentComplaintPortal.Application/DTOs/CategoryDto.cs` ✅ DONE
- Added `Icon` property
- Added `Color` property

**File**: `src/StudentComplaintPortal.Application/DTOs/CreateCategoryDto.cs` ✅ DONE
- Added `Icon` property (optional)
- Added `Color` property (optional)

#### **3. Service Layer**
**File**: `src/StudentComplaintPortal.Application/Services/CategoryService.cs` ✅ DONE (PARTIALLY)
- Updated `CreateCategoryAsync()` to accept Icon and Color
- Updated `MapToDto()` to include Icon and Color
- **STILL NEEDED**: Update the `UpdateCategory()` method in CategoriesController to handle Icon/Color updates

#### **4. Controller Update**
**File**: `src/StudentComplaintPortal.Web/Controllers/Api/CategoriesController.cs`
**CHANGES NEEDED**:
- In the `UpdateCategory()` method (line ~170), add:
  ```csharp
  category.Icon = dto.Icon ?? category.Icon;
  category.Color = dto.Color ?? category.Color;
  ```

#### **5. CategoryManagement View - Create Tab**
**File**: `src/StudentComplaintPortal.Web/Views/Dashboard/CategoryManagement.cshtml`
**CHANGES NEEDED** (in Create section):
- After description field, add:
  ```html
  <!-- Icon Selection -->
  <div class="mb-3">
      <label class="form-label fw-bold">Category Icon</label>
      <div class="icon-selector">
          <button type="button" class="icon-btn" onclick="selectIcon(this, '🎓')">🎓</button>
          <button type="button" class="icon-btn" onclick="selectIcon(this, '💰')">💰</button>
          <button type="button" class="icon-btn" onclick="selectIcon(this, '🏫')">🏫</button>
          <button type="button" class="icon-btn" onclick="selectIcon(this, '🔒')">🔒</button>
          <button type="button" class="icon-btn" onclick="selectIcon(this, '🏠')">🏠</button>
          <button type="button" class="icon-btn" onclick="selectIcon(this, '📋')">📋</button>
          <button type="button" class="icon-btn" onclick="selectIcon(this, '❓')">❓</button>
      </div>
      <input type="hidden" id="categoryIcon" value="📋" />
      <small class="text-muted">Selected: <span id="iconPreview">📋</span></small>
  </div>

  <!-- Color Selection -->
  <div class="mb-3">
      <label class="form-label fw-bold">Category Color</label>
      <div class="color-selector">
          <input type="color" id="categoryColor" class="form-control form-control-color" value="#007bff" style="width: 100px; height: 40px;" />
      </div>
      <small class="text-muted">Pick a color for category badge</small>
  </div>
  ```

#### **6. CategoryManagement View - Edit Tab**
**File**: `src/StudentComplaintPortal.Web/Views/Dashboard/CategoryManagement.cshtml`
**CHANGES NEEDED** (in Edit section):
- Add same icon selector and color picker for edit form
- Use `editCategoryIcon` and `editCategoryColor` IDs instead

#### **7. JavaScript Updates in CategoryManagement View**
**CHANGES NEEDED** in the `<script>` section:

Add new function:
```javascript
function selectIcon(button, icon) {
    document.querySelectorAll('.icon-btn').forEach(btn => btn.classList.remove('active'));
    button.classList.add('active');
    document.getElementById('categoryIcon').value = icon;
    document.getElementById('iconPreview').textContent = icon;
}

function selectEditIcon(button, icon) {
    document.querySelectorAll('.edit-icon-btn').forEach(btn => btn.classList.remove('active'));
    button.classList.add('active');
    document.getElementById('editCategoryIcon').value = icon;
    document.getElementById('editIconPreview').textContent = icon;
}
```

Update `submitCategory()` function to include:
```javascript
const icon = document.getElementById('categoryIcon').value;
const color = document.getElementById('categoryColor').value;
const payload = { name, description, icon, color, assigneeIds, attachmentRules };
```

Update `updateCategory()` function to include:
```javascript
const icon = document.getElementById('editCategoryIcon').value;
const color = document.getElementById('editCategoryColor').value;
const payload = { name, description, icon, color, assigneeIds, attachmentRules };
```

Update `loadEditForm()` to populate icon and color:
```javascript
document.getElementById('editCategoryIcon').value = category.icon || '📋';
document.getElementById('editCategoryColor').value = category.color || '#007bff';
document.getElementById('editIconPreview').textContent = category.icon || '📋';
```

#### **8. CSS Styling**
**File**: `src/StudentComplaintPortal.Web/wwwroot/css/site.css` (or similar)
**ADD**:
```css
.icon-selector, .edit-icon-selector {
    display: flex;
    gap: 10px;
    flex-wrap: wrap;
    margin-bottom: 10px;
}

.icon-btn, .edit-icon-btn {
    font-size: 28px;
    padding: 10px;
    border: 2px solid #ddd;
    border-radius: 5px;
    background: white;
    cursor: pointer;
    transition: all 0.3s ease;
}

.icon-btn:hover, .edit-icon-btn:hover {
    transform: scale(1.1);
    border-color: #007bff;
}

.icon-btn.active, .edit-icon-btn.active {
    border-color: #007bff;
    background: #e7f1ff;
}

.color-selector {
    display: flex;
    align-items: center;
    gap: 10px;
}
```

#### **9. Display Categories with Icons**
**Files to update to show icons**:
- `CategoryManagement.cshtml` - Show icon in table
- `StudentIndex.cshtml` - Show icon in complaint cards
- `NewComplaint.cshtml` - Show icon when selecting category dropdown

**Example change**:
```html
<!-- In category list table -->
<td><span style="font-size: 24px; margin-right: 8px;">@cat.icon</span><strong>@cat.name</strong></td>

<!-- In category badge -->
<span class="badge" style="background-color: @cat.color;">@cat.icon @cat.name</span>
```

---

## **FEATURE 2: TYPING INDICATORS**

### Changes Required:

#### **1. ChatHub SignalR Hub**
**File**: `src/StudentComplaintPortal.Web/Hubs/ChatHub.cs`

**ADD new methods**:
```csharp
public async Task UserStartedTyping(int complaintId)
{
    var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    var userName = Context.User?.FindFirst(ClaimTypes.GivenName)?.Value ?? "User";
    
    if (!string.IsNullOrEmpty(userId))
    {
        await Clients.Group($"complaint-{complaintId}")
            .SendAsync("UserTyping", userId, userName);
    }
}

public async Task UserStoppedTyping(int complaintId)
{
    var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    
    if (!string.IsNullOrEmpty(userId))
    {
        await Clients.Group($"complaint-{complaintId}")
            .SendAsync("UserStoppedTyping", userId);
    }
}

public async Task JoinComplaintGroup(int complaintId)
{
    await Groups.AddToGroupAsync(Context.ConnectionId, $"complaint-{complaintId}");
}

public async Task LeaveComplaintGroup(int complaintId)
{
    await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"complaint-{complaintId}");
}
```

#### **2. ChatWorkspace View Update**
**File**: `src/StudentComplaintPortal.Web/Views/ChatWorkspace/Index.cshtml`

**ADD HTML for typing indicator** (in chat input section):
```html
<!-- Add this before or after message input -->
<div id="typingIndicator" style="display:none; padding: 10px; color: #999; font-size: 12px; font-style: italic;">
    <span id="typingText"></span>
</div>
```

#### **3. Chat Workspace JavaScript**
**File**: `src/StudentComplaintPortal.Web/wwwroot/js/chat-workspace.js`

**ADD in init() function**:
```javascript
connection.on("UserTyping", (userId, userName) => {
    if (currentChatId) {
        showTypingIndicator(userName);
    }
});

connection.on("UserStoppedTyping", (userId) => {
    hideTypingIndicator();
});
```

**ADD new functions**:
```javascript
let typingTimeout = null;
let typingSet = new Set();

function notifyTyping() {
    if (currentChatType === 'complaint' && currentChatId) {
        connection.invoke("UserStartedTyping", currentChatId).catch(err => console.error(err));
    } else if (currentChatType === 'internal' && currentChatId) {
        connection.invoke("UserStartedTyping", currentChatId).catch(err => console.error(err));
    }
    
    clearTimeout(typingTimeout);
    typingTimeout = setTimeout(() => {
        if (currentChatId) {
            connection.invoke("UserStoppedTyping", currentChatId).catch(err => console.error(err));
        }
    }, 3000); // Stop typing indicator after 3 seconds of inactivity
}

function showTypingIndicator(userName) {
    typingSet.add(userName);
    const typingNames = Array.from(typingSet).join(", ");
    const indicator = document.getElementById('typingIndicator');
    if (indicator) {
        document.getElementById('typingText').textContent = 
            typingNames + (typingSet.size === 1 ? " is typing..." : " are typing...");
        indicator.style.display = 'block';
    }
}

function hideTypingIndicator() {
    typingSet.clear();
    const indicator = document.getElementById('typingIndicator');
    if (indicator) {
        indicator.style.display = 'none';
    }
}
```

#### **4. Message Input Event Listener**
**File**: `src/StudentComplaintPortal.Web/wwwroot/js/chat-workspace.js`

**MODIFY setupMessageInputToggle()** function to add typing indicator:
```javascript
function setupMessageInputToggle() {
    const input = document.getElementById('messageInput');
    if (!input) return;

    input.addEventListener('input', () => {
        const hasText = input.value.trim().length > 0;
        document.getElementById('micButton').style.display = hasText ? 'none' : 'flex';
        document.getElementById('sendButton').style.display = hasText ? 'flex' : 'none';
        
        // ADDED: Notify typing
        notifyTyping();
    });

    input.addEventListener('keydown', (e) => {
        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
            sendMessage();
            // ADDED: Stop typing indicator after send
            connection.invoke("UserStoppedTyping", currentChatId)
                .catch(err => console.error(err));
        }
    });
}
```

#### **5. Join/Leave Complaint Group**
**File**: `src/StudentComplaintPortal.Web/wwwroot/js/chat-workspace.js`

**IN openComplaintChat() function, ADD**:
```javascript
// After chat is opened
connection.invoke("JoinComplaintGroup", complaintId).catch(err => console.error(err));
```

**IN when chat is closed, ADD**:
```javascript
// Before switching chats
if (currentChatId) {
    connection.invoke("LeaveComplaintGroup", currentChatId).catch(err => console.error(err));
}
```

#### **6. CSS for Typing Indicator**
**File**: `src/StudentComplaintPortal.Web/wwwroot/css/site.css`

**ADD**:
```css
#typingIndicator {
    border-top: 1px solid #e0e0e0;
    background: #f9f9f9;
}

.typing-dots {
    display: inline-flex;
    gap: 3px;
}

.typing-dots span {
    width: 8px;
    height: 8px;
    border-radius: 50%;
    background: #999;
    animation: typing 1.4s infinite;
}

.typing-dots span:nth-child(2) {
    animation-delay: 0.2s;
}

.typing-dots span:nth-child(3) {
    animation-delay: 0.4s;
}

@keyframes typing {
    0%, 60%, 100% {
        opacity: 0.5;
        transform: translateY(0);
    }
    30% {
        opacity: 1;
        transform: translateY(-10px);
    }
}
```

---

## **DATABASE MIGRATION**

**File to create**: `src/StudentComplaintPortal.Data/Migrations/[timestamp]_AddCategoryIconAndColor.cs`

```csharp
public partial class AddCategoryIconAndColor : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Icon",
            table: "Categories",
            type: "nvarchar(50)",
            nullable: true,
            defaultValue: "📋");

        migrationBuilder.AddColumn<string>(
            name: "Color",
            table: "Categories",
            type: "nvarchar(7)",
            nullable: true,
            defaultValue: "#007bff");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "Icon", table: "Categories");
        migrationBuilder.DropColumn(name: "Color", table: "Categories");
    }
}
```

---

## **SUMMARY OF CHANGES**

### **Files to Create:**
1. `src/StudentComplaintPortal.Data/Migrations/[timestamp]_AddCategoryIconAndColor.cs`

### **Files to Modify:**
1. ✅ `src/StudentComplaintPortal.Domain/Entities/Category.cs` 
2. ✅ `src/StudentComplaintPortal.Application/DTOs/CategoryDto.cs`
3. ✅ `src/StudentComplaintPortal.Application/DTOs/CreateCategoryDto.cs`
4. ✅ `src/StudentComplaintPortal.Application/Services/CategoryService.cs` (PARTIAL - needs UpdateCategory)
5. `src/StudentComplaintPortal.Web/Controllers/Api/CategoriesController.cs` (UpdateCategory method)
6. `src/StudentComplaintPortal.Web/Views/Dashboard/CategoryManagement.cshtml` (Add icon/color picker UI)
7. `src/StudentComplaintPortal.Web/Views/StudentIndex.cshtml` (Display icons in complaints)
8. `src/StudentComplaintPortal.Web/Views/Dashboard/NewComplaint.cshtml` (Display icons in category dropdown)
9. `src/StudentComplaintPortal.Web/Hubs/ChatHub.cs` (Add typing indicator methods)
10. `src/StudentComplaintPortal.Web/Views/ChatWorkspace/Index.cshtml` (Add typing indicator UI)
11. `src/StudentComplaintPortal.Web/wwwroot/js/chat-workspace.js` (Add typing logic)
12. `src/StudentComplaintPortal.Web/wwwroot/css/site.css` (Add styling)

### **Total Implementation Items:**
- **12 Files to Modify/Create**
- **2 Major Features**
- **Estimated Implementation Time: 2-3 hours**

---

## **TESTING CHECKLIST**

✅ Create category with custom icon and color
✅ Edit category to change icon/color
✅ View categories with visual icons in table
✅ View icons in complaint cards
✅ View icons in category dropdown when creating complaint
✅ Type in chat - see "User is typing..." indicator
✅ Multiple users typing - shows "User1, User2 are typing..."
✅ Stop typing after 3 seconds - indicator disappears
✅ Send message - typing indicator clears
✅ Different complaint chats have separate typing indicators

---

**Status**: Ready for implementation ✅
**Complexity**: Medium
**Risk Level**: Low
