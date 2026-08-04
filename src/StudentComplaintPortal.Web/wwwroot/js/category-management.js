// Category Management JavaScript Functions

/**
 * Toggle category active status
 * @param {number} categoryId - The ID of the category to toggle
 */
async function toggleCategoryActive(categoryId) {
    try {
        const response = await fetch(`/api/categories/${categoryId}/toggle-active`, {
            method: 'PATCH',
            headers: {
                'Content-Type': 'application/json'
            }
        });

        if (response.ok) {
            const result = await response.json();
            alert(result.message);
            location.reload(); // Reload to reflect changes
        } else {
            alert('Failed to toggle category status');
        }
    } catch (error) {
        console.error('Error toggling category status:', error);
        alert('An error occurred while toggling the category status.');
    }
}

/**
 * Delete category (soft delete via API)
 * @param {number} categoryId - The ID of the category to delete
 */
async function deleteCategory_API(categoryId) {
    try {
        const response = await fetch(`/api/categories/${categoryId}`, {
            method: 'DELETE',
            headers: {
                'Content-Type': 'application/json'
            }
        });

        if (response.ok) {
            const result = await response.json();
            alert(result.message);
            location.reload(); // Reload to reflect changes
        } else {
            alert('Failed to delete category');
        }
    } catch (error) {
        console.error('Error deleting category:', error);
        alert('An error occurred while deleting the category.');
    }
}

/**
 * Submit category form (used in category creation/management page)
 * @param {string} actionUrl - The URL to submit to
 */
async function submitCategory(actionUrl = '/api/categories') {
    const categoryName = document.getElementById('categoryName')?.value;
    const categoryDescription = document.getElementById('categoryDescription')?.value;
    const categoryAssignees = document.getElementById('categoryAssignees');
    const selectedAssignees = Array.from(categoryAssignees?.selectedOptions || []).map(o => o.value);

    if (!categoryName || categoryName.trim() === '') {
        alert('Please enter a category name.');
        return;
    }

    // Gather attachment rules
    const rules = [];
    document.querySelectorAll('.rule-row').forEach(row => {
        const fileType = row.querySelector('.rule-type')?.value;
        const maxCount = parseInt(row.querySelector('.rule-max-count')?.value) || 1;
        const maxSize = parseInt(row.querySelector('.rule-max-size')?.value) || 5242880;
        const isRequired = row.querySelector('.rule-required')?.checked || false;

        if (fileType) {
            rules.push({
                fileType: fileType,
                maxFileCount: maxCount,
                maxFileSizeBytes: maxSize,
                isRequired: isRequired
            });
        }
    });

    const categoryData = {
        name: categoryName,
        description: categoryDescription || null,
        assigneeIds: selectedAssignees,
        attachmentRules: rules
    };

    try {
        const response = await fetch(actionUrl, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(categoryData)
        });

        if (response.ok) {
            const result = await response.json();
            alert('Category created successfully!');
            // Optionally redirect or refresh
            location.href = '/Dashboard/Categories';
        } else {
            const errorData = await response.json();
            alert(`Error: ${errorData.message || 'Failed to create category'}`);
        }
    } catch (error) {
        console.error('Error:', error);
        alert('An error occurred while creating the category.');
    }
}

/**
 * Add a new attachment rule row in forms
 * @param {string} fileType - Optional file type to pre-select
 * @param {number} maxCount - Optional max file count
 * @param {number} maxSize - Optional max file size in bytes
 * @param {boolean} isRequired - Optional required flag
 */
function addRuleRow(fileType = '', maxCount = 1, maxSize = 5242880, isRequired = false) {
    const container = document.getElementById('rulesContainer');
    if (!container) return;

    const ruleDiv = document.createElement('div');
    ruleDiv.className = 'card mb-2 p-3 rule-row';
    ruleDiv.innerHTML = `
        <div class="row">
            <div class="col-md-3">
                <label class="form-label">File Type</label>
                <select class="form-select form-select-sm rule-type" required>
                    <option value="" ${!fileType ? 'selected' : ''}>Select...</option>
                    <option value="PDF" ${fileType === 'PDF' ? 'selected' : ''}>PDF</option>
                    <option value="Image" ${fileType === 'Image' ? 'selected' : ''}>Image</option>
                    <option value="Document" ${fileType === 'Document' ? 'selected' : ''}>Document</option>
                    <option value="Video" ${fileType === 'Video' ? 'selected' : ''}>Video</option>
                    <option value="Audio" ${fileType === 'Audio' ? 'selected' : ''}>Audio</option>
                </select>
            </div>
            <div class="col-md-3">
                <label class="form-label">Max Count</label>
                <input type="number" class="form-control form-control-sm rule-max-count" value="${maxCount}" min="1" required />
            </div>
            <div class="col-md-3">
                <label class="form-label">Max Size (bytes)</label>
                <input type="number" class="form-control form-control-sm rule-max-size" value="${maxSize}" min="1" required />
            </div>
            <div class="col-md-2">
                <label class="form-label">Required</label>
                <div class="form-check mt-2">
                    <input class="form-check-input rule-required" type="checkbox" ${isRequired ? 'checked' : ''} />
                </div>
            </div>
            <div class="col-md-1 d-flex align-items-end">
                <button type="button" class="btn btn-outline-danger btn-sm w-100" onclick="removeRuleRow(this)">
                    <i class="bi bi-trash"></i>
                </button>
            </div>
        </div>
    `;
    container.appendChild(ruleDiv);
}

/**
 * Remove an attachment rule row
 * @param {HTMLElement} btn - The remove button element
 */
function removeRuleRow(btn) {
    const ruleRow = btn.closest('.rule-row');
    if (ruleRow) {
        ruleRow.remove();
    }
}

/**
 * Validate category form before submission
 * @returns {boolean} - True if form is valid
 */
function validateCategoryForm() {
    const categoryName = document.getElementById('categoryName')?.value;

    if (!categoryName || categoryName.trim() === '') {
        alert('Category name is required.');
        return false;
    }

    if (categoryName.trim().length < 2) {
        alert('Category name must be at least 2 characters long.');
        return false;
    }

    return true;
}

/**
 * Format file size in human-readable format
 * @param {number} bytes - Size in bytes
 * @returns {string} - Formatted size string
 */
function formatBytes(bytes) {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return Math.round((bytes / Math.pow(k, i)) * 100) / 100 + ' ' + sizes[i];
}

/**
 * Escape HTML special characters
 * @param {string} text - Text to escape
 * @returns {string} - Escaped text
 */
function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

/**
 * Select an icon for the category
 * @param {string} iconValue - The icon value to select
 * @param {HTMLElement} buttonElement - The button element that was clicked
 */
function selectIcon(iconValue, buttonElement) {
    // Remove active class from all icon buttons
    document.querySelectorAll('.icon-btn').forEach(btn => {
        btn.classList.remove('active');
    });

    // Add active class to clicked button
    buttonElement.classList.add('active');

    // Update hidden input
    const selectedIconInput = document.getElementById('selectedIcon');
    if (selectedIconInput) {
        selectedIconInput.value = iconValue;
    }
}

/**
 * Initialize icon picker on page load
 */
function initializeIconPicker() {
    const selectedIcon = document.getElementById('selectedIcon')?.value;
    if (selectedIcon) {
        const button = document.querySelector(`[data-icon="${selectedIcon}"]`);
        if (button) {
            button.classList.add('active');
        }
    }
}

// Initialize on DOM ready
document.addEventListener('DOMContentLoaded', function() {
    initializeIconPicker();
});

// Export functions for use in views
if (typeof module !== 'undefined' && module.exports) {
    module.exports = {
        toggleCategoryActive,
        deleteCategory_API,
        submitCategory,
        addRuleRow,
        removeRuleRow,
        validateCategoryForm,
        formatBytes,
        escapeHtml,
        selectIcon,
        initializeIconPicker
    };
}
