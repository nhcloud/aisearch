// Site.js - General utilities and enhancements

// Enhance Bootstrap tooltips and popovers
$(function () {
    $('[data-bs-toggle="tooltip"]').tooltip();
    $('[data-bs-toggle="popover"]').popover();
});

// Add CSRF token to all AJAX requests
$(function () {
    const token = $('input[name="__RequestVerificationToken"]').val();
    if (token) {
        $.ajaxSetup({
            beforeSend: function (xhr) {
                xhr.setRequestHeader("RequestVerificationToken", token);
            }
        });
    }
});

// Auto-hide alerts after a certain time
$(function () {
    $('.alert-success, .alert-info').each(function () {
        const alert = $(this);
        if (!alert.hasClass('alert-persistent')) {
            setTimeout(function () {
                alert.fadeOut();
            }, 5000);
        }
    });
});

// Enhanced file input styling
$(function () {
    $('.custom-file-input').on('change', function () {
        const fileName = $(this).val().split('\\').pop();
        $(this).next('.custom-file-label').addClass("selected").html(fileName);
    });
});

// Smooth scrolling for anchor links
$(function () {
    $('a[href*="#"]:not([href="#"])').click(function () {
        if (location.pathname.replace(/^\//, '') == this.pathname.replace(/^\//, '') && location.hostname == this.hostname) {
            var target = $(this.hash);
            target = target.length ? target : $('[name=' + this.hash.slice(1) + ']');
            if (target.length) {
                $('html, body').animate({
                    scrollTop: target.offset().top - 70
                }, 1000);
                return false;
            }
        }
    });
});

// Add loading state to buttons
function addLoadingState(button, text = 'Loading...') {
    const $btn = $(button);
    const originalText = $btn.html();
    
    $btn.data('original-text', originalText);
    $btn.html(`
        <span class="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>
        ${text}
    `).prop('disabled', true);
    
    return function removeLoadingState() {
        $btn.html($btn.data('original-text')).prop('disabled', false);
    };
}

// Utility function to show toast notifications
function showToast(message, type = 'info') {
    const toastHtml = `
        <div class="toast align-items-center text-white bg-${type} border-0" role="alert" aria-live="assertive" aria-atomic="true">
            <div class="d-flex">
                <div class="toast-body">
                    ${message}
                </div>
                <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>
            </div>
        </div>
    `;
    
    // Create toast container if it doesn't exist
    if ($('#toast-container').length === 0) {
        $('body').append('<div id="toast-container" class="toast-container position-fixed top-0 end-0 p-3"></div>');
    }
    
    const $toast = $(toastHtml);
    $('#toast-container').append($toast);
    
    const toast = new bootstrap.Toast($toast[0]);
    toast.show();
    
    // Remove toast element after it's hidden
    $toast.on('hidden.bs.toast', function () {
        $(this).remove();
    });
}

// Enhanced form validation
function validateForm(formSelector) {
    const $form = $(formSelector);
    let isValid = true;
    
    $form.find('input[required], textarea[required], select[required]').each(function () {
        const $field = $(this);
        const value = $field.val().trim();
        
        if (!value) {
            $field.addClass('is-invalid');
            isValid = false;
        } else {
            $field.removeClass('is-invalid').addClass('is-valid');
        }
    });
    
    // Email validation
    $form.find('input[type="email"]').each(function () {
        const $field = $(this);
        const email = $field.val().trim();
        const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
        
        if (email && !emailRegex.test(email)) {
            $field.addClass('is-invalid');
            isValid = false;
        }
    });
    
    return isValid;
}

// Debounce function for search inputs
function debounce(func, wait, immediate) {
    let timeout;
    return function executedFunction() {
        const context = this;
        const args = arguments;
        const later = function () {
            timeout = null;
            if (!immediate) func.apply(context, args);
        };
        const callNow = immediate && !timeout;
        clearTimeout(timeout);
        timeout = setTimeout(later, wait);
        if (callNow) func.apply(context, args);
    };
}

// Copy to clipboard functionality
function copyToClipboard(text) {
    if (navigator.clipboard) {
        navigator.clipboard.writeText(text).then(function () {
            showToast('Copied to clipboard!', 'success');
        }).catch(function () {
            fallbackCopyToClipboard(text);
        });
    } else {
        fallbackCopyToClipboard(text);
    }
}

function fallbackCopyToClipboard(text) {
    const textArea = document.createElement("textarea");
    textArea.value = text;
    textArea.style.position = "fixed";
    textArea.style.left = "-999999px";
    textArea.style.top = "-999999px";
    document.body.appendChild(textArea);
    textArea.focus();
    textArea.select();
    
    try {
        document.execCommand('copy');
        showToast('Copied to clipboard!', 'success');
    } catch (err) {
        showToast('Failed to copy to clipboard', 'danger');
    }
    
    document.body.removeChild(textArea);
}

// Enhanced table sorting
function makeSortable(tableSelector) {
    $(tableSelector + ' th').css('cursor', 'pointer').click(function () {
        const table = $(this).parents('table').eq(0);
        const rows = table.find('tr:gt(0)').toArray().sort(comparer($(this).index()));
        
        this.asc = !this.asc;
        if (!this.asc) {
            rows.reverse();
        }
        
        for (let i = 0; i < rows.length; i++) {
            table.append(rows[i]);
        }
        
        // Update sort indicators
        table.find('th .sort-indicator').remove();
        $(this).append(`<span class="sort-indicator ms-1">${this.asc ? '↑' : '↓'}</span>`);
    });
}

function comparer(index) {
    return function (a, b) {
        const valA = getCellValue(a, index);
        const valB = getCellValue(b, index);
        return $.isNumeric(valA) && $.isNumeric(valB) ? valA - valB : valA.toString().localeCompare(valB);
    };
}

function getCellValue(row, index) {
    return $(row).children('td').eq(index).text();
}

// Enhanced image preview functionality
function previewImage(input, previewElement) {
    if (input.files && input.files[0]) {
        const reader = new FileReader();
        reader.onload = function (e) {
            $(previewElement).attr('src', e.target.result).show();
        };
        reader.readAsDataURL(input.files[0]);
    }
}

// Dark mode toggle functionality
function initDarkMode() {
    const darkModeToggle = $('#darkModeToggle');
    const isDarkMode = localStorage.getItem('darkMode') === 'true';
    
    if (isDarkMode) {
        $('body').addClass('dark-mode');
        darkModeToggle.prop('checked', true);
    }
    
    darkModeToggle.change(function () {
        const isDark = $(this).is(':checked');
        $('body').toggleClass('dark-mode', isDark);
        localStorage.setItem('darkMode', isDark);
    });
}

// Initialize components when document is ready
$(function () {
    // Initialize dark mode if toggle exists
    if ($('#darkModeToggle').length) {
        initDarkMode();
    }
    
    // Make tables sortable if they have the sortable class
    $('.sortable-table').each(function () {
        makeSortable('#' + $(this).attr('id'));
    });
    
    // Auto-resize textareas
    $('textarea.auto-resize').on('input', function () {
        this.style.height = 'auto';
        this.style.height = (this.scrollHeight) + 'px';
    });
});

// Export functions for global use
window.showToast = showToast;
window.copyToClipboard = copyToClipboard;
window.addLoadingState = addLoadingState;
window.validateForm = validateForm;
window.debounce = debounce;
window.previewImage = previewImage;
