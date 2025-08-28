// Global variables
let chatHistory = [];
let currentSearchConfig = {
    UseKnowledgeAgent: false, // Changed from useKnowledgeAgent to UseKnowledgeAgent
    Top: 10,                  // Changed from top to Top
    IncludeImages: true,      // Changed from includeImages to IncludeImages
    IncludeText: true,        // Changed from includeText to IncludeText
    Threshold: 0.7           // Changed from threshold to Threshold
};

// Speech recognition variables
let speechRecognition = null;
let isRecording = false;
let speechSynthesis = window.speechSynthesis;
let currentUtterance = null;
let isSpeaking = false;
let currentSpeakingMessageId = null;

// Initialize app when DOM is ready
$(document).ready(function() {
    initializeApp();
});

function initializeApp() {
    setupEventListeners();
    setupSliders();
    setupSpeechFeatures();
    loadInitialData();
}

function setupEventListeners() {
    // Search
    $('#searchBtn').click(performSearch);
    $('#searchQuery').keypress(function(e) {
        if (e.which === 13) performSearch();
    });

    // Chat
    $('#sendBtn').click(sendChatMessage);
    
    // Chat message speak button event delegation - use more specific selector
    $(document).on('click', '.speak-btn[data-message-id]', function(e) {
        e.preventDefault();
        e.stopPropagation();
        
        const messageId = $(this).data('message-id');
        console.log('Speak button clicked via delegation:', messageId);
        
        if (messageId) {
            const messageCard = $(`.card[data-message-id="${messageId}"]`);
            const messageContent = messageCard.find('.message-content');
            
            // Get clean text content
            let text = messageContent.text().trim();
            
            // Fallback to HTML content if text is empty
            if (!text) {
                text = messageContent.html().replace(/<[^>]*>/g, '').trim();
            }
            
            handleSpeakButtonClick(messageId, text);
        } else {
            console.error('No message ID found for speak button');
        }
    });
    
    // Documents
    $('#createIndexForm').submit(function(e) {
        e.preventDefault();
        createIndex();
    });

    // Tab change events
    $('button[data-bs-toggle="tab"]').on('shown.bs.tab', function(e) {
        const target = $(e.target).attr('data-bs-target');
        if (target === '#documents') {
            refreshDocuments();
        } else if (target === '#indexes') {
            refreshIndexes();
        }
    });
    
    console.log('Event listeners setup complete');
}

function setupSliders() {
    // Search sliders
    $('#topResults').on('input', function() {
        const value = $(this).val();
        $('#topResultsValue').text(value);
        currentSearchConfig.Top = parseInt(value); // Updated property name
    });

    $('#threshold').on('input', function() {
        const value = $(this).val();
        $('#thresholdValue').text(value);
        currentSearchConfig.Threshold = parseFloat(value); // Updated property name
    });

    // Chat sliders
    $('#chatTopResults').on('input', function() {
        $('#chatTopResultsValue').text($(this).val());
    });

    $('#chatThreshold').on('input', function() {
        $('#chatThresholdValue').text($(this).val());
    });

    // Speech sliders
    $('#speechRate').on('input', function() {
        $('#speechRateValue').text($(this).val());
    });

    $('#speechPitch').on('input', function() {
        $('#speechPitchValue').text($(this).val());
    });
}

function setupSpeechFeatures() {
    // Check for speech recognition support
    if ('webkitSpeechRecognition' in window || 'SpeechRecognition' in window) {
        const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
        speechRecognition = new SpeechRecognition();
        
        speechRecognition.continuous = false;
        speechRecognition.interimResults = false;
        speechRecognition.maxAlternatives = 1;
        speechRecognition.lang = $('#speechLanguage').val() || 'en-US';

        // Update language when changed
        $('#speechLanguage').on('change', function() {
            if (speechRecognition) {
                speechRecognition.lang = $(this).val();
            }
        });

        speechRecognition.onstart = function() {
            isRecording = true;
            $('#micIcon').text('mic_off');
            $('#micBtn').removeClass('btn-outline-secondary').addClass('btn-danger');
            $('#speechStatus').removeClass('d-none');
            $('#speechStatusText').text('Listening...');
        };

        speechRecognition.onresult = function(event) {
            const transcript = event.results[0][0].transcript;
            $('#chatInput').val(transcript);
            $('#speechStatusText').text('Speech recognized: "' + transcript + '"');
            
            // Auto-send after a short delay if the user doesn't modify the text
            setTimeout(() => {
                if ($('#chatInput').val() === transcript) {
                    $('#speechStatus').addClass('d-none');
                }
            }, 2000);
        };

        speechRecognition.onerror = function(event) {
            console.error('Speech recognition error:', event.error);
            $('#speechStatusText').text('Error: ' + event.error);
            setTimeout(() => {
                $('#speechStatus').addClass('d-none');
            }, 3000);
        };

        speechRecognition.onend = function() {
            isRecording = false;
            $('#micIcon').text('mic');
            $('#micBtn').removeClass('btn-danger').addClass('btn-outline-secondary');
        };

        // Microphone button events
        $('#micBtn').on('mousedown touchstart', function(e) {
            e.preventDefault();
            startSpeechRecognition();
        });

        $('#micBtn').on('mouseup mouseleave touchend', function(e) {
            e.preventDefault();
            stopSpeechRecognition();
        });

        // Keyboard shortcut for speech recognition (Ctrl+Shift+M)
        $(document).on('keydown', function(e) {
            if (e.ctrlKey && e.shiftKey && e.key === 'M') {
                e.preventDefault();
                if (!isRecording) {
                    startSpeechRecognition();
                }
            }
        });

        $(document).on('keyup', function(e) {
            if (e.ctrlKey && e.shiftKey && e.key === 'M') {
                e.preventDefault();
                stopSpeechRecognition();
            }
        });

    } else {
        // Speech recognition not supported
        $('#micBtn').prop('disabled', true).attr('title', 'Speech recognition not supported in this browser');
        console.warn('Speech recognition not supported in this browser');
    }

    // Check for speech synthesis support
    if (!('speechSynthesis' in window)) {
        $('#chatEnableTextToSpeech').prop('disabled', true);
        console.warn('Speech synthesis not supported in this browser');
    }
}

function startSpeechRecognition() {
    if (speechRecognition && !isRecording) {
        try {
            speechRecognition.lang = $('#speechLanguage').val() || 'en-US';
            speechRecognition.start();
        } catch (error) {
            console.error('Error starting speech recognition:', error);
            showAlert('chatError', 'Error starting speech recognition: ' + error.message);
        }
    }
}

function stopSpeechRecognition() {
    if (speechRecognition && isRecording) {
        try {
            speechRecognition.stop();
        } catch (error) {
            console.error('Error stopping speech recognition:', error);
        }
    }
}

function updateSpeakButtonState(messageId, speaking) {
    if (!messageId) return;
    
    console.log('Updating speak button state:', messageId, 'speaking:', speaking);
    
    const button = $(`.speak-btn[data-message-id="${messageId}"]`);
    const messageCard = $(`.card[data-message-id="${messageId}"]`);
    
    if (button.length > 0) {
        const icon = button.find('i');
        if (speaking) {
            icon.text('stop');
            button.removeClass('btn-outline-secondary').addClass('btn-outline-danger');
            button.attr('title', 'Stop speaking');
            
            // Add speaking highlight to message
            messageCard.addClass('speaking-message');
            console.log('Button updated to stop state for message:', messageId);
        } else {
            icon.text('volume_up');
            button.removeClass('btn-outline-danger').addClass('btn-outline-secondary');
            button.attr('title', 'Read message aloud');
            
            // Remove speaking highlight from message
            messageCard.removeClass('speaking-message');
            console.log('Button updated to speak state for message:', messageId);
        }
    } else {
        console.warn('Speak button not found for message:', messageId);
    }
}

function speakText(text, messageId = null) {
    if (!speechSynthesis || !text) {
        console.warn('Speech synthesis not available or no text provided');
        return;
    }

    console.log('Starting speech synthesis for message:', messageId, 'text length:', text.length);

    // Stop any current speech
    stopTextToSpeech();

    // Create new utterance
    currentUtterance = new SpeechSynthesisUtterance(text);
    currentSpeakingMessageId = messageId;
    
    // Set speech parameters
    currentUtterance.rate = parseFloat($('#speechRate').val()) || 1.0;
    currentUtterance.pitch = parseFloat($('#speechPitch').val()) || 1.0;
    currentUtterance.lang = $('#speechLanguage').val() || 'en-US';

    // Event handlers
    currentUtterance.onstart = function() {
        console.log('Speech synthesis started for message:', messageId);
        isSpeaking = true;
        updateSpeakButtonState(messageId, true);
        showGlobalSpeechControls();
    };

    currentUtterance.onend = function() {
        console.log('Speech synthesis ended for message:', messageId);
        isSpeaking = false;
        currentUtterance = null;
        currentSpeakingMessageId = null;
        updateSpeakButtonState(messageId, false);
        hideGlobalSpeechControls();
    };

    currentUtterance.onerror = function(event) {
        console.error('Speech synthesis error for message:', messageId, event.error);
        isSpeaking = false;
        currentUtterance = null;
        currentSpeakingMessageId = null;
        updateSpeakButtonState(messageId, false);
        hideGlobalSpeechControls();
    };

    // Speak the text
    speechSynthesis.speak(currentUtterance);
}

function stopTextToSpeech() {
    console.log('Stopping text-to-speech. Current speaking message:', currentSpeakingMessageId);
    
    if (speechSynthesis && speechSynthesis.speaking) {
        speechSynthesis.cancel();
    }
    if (currentSpeakingMessageId) {
        updateSpeakButtonState(currentSpeakingMessageId, false);
    }
    isSpeaking = false;
    currentUtterance = null;
    currentSpeakingMessageId = null;
    hideGlobalSpeechControls();
}

function pauseTextToSpeech() {
    if (speechSynthesis && speechSynthesis.speaking) {
        if (speechSynthesis.paused) {
            // Resume if paused
            speechSynthesis.resume();
            updateGlobalSpeechControlsText('Speaking...');
            updatePauseButton(false);
        } else {
            // Pause if speaking
            speechSynthesis.pause();
            updateGlobalSpeechControlsText('Paused');
            updatePauseButton(true);
        }
    }
}

function resumeTextToSpeech() {
    if (speechSynthesis && speechSynthesis.paused) {
        speechSynthesis.resume();
        updateGlobalSpeechControlsText('Speaking...');
        updatePauseButton(false);
    }
}

function updatePauseButton(isPaused) {
    const pauseBtn = $('#globalSpeechControls').find('button[onclick="pauseTextToSpeech()"]');
    const icon = pauseBtn.find('i');
    
    if (isPaused) {
        icon.text('play_arrow');
        pauseBtn.attr('title', 'Resume speech');
        pauseBtn.removeClass('btn-outline-warning').addClass('btn-outline-success');
    } else {
        icon.text('pause');
        pauseBtn.attr('title', 'Pause speech');
        pauseBtn.removeClass('btn-outline-success').addClass('btn-outline-warning');
    }
}

function showGlobalSpeechControls() {
    $('#globalSpeechControls').removeClass('d-none');
    updateGlobalSpeechControlsText('Speaking...');
    updatePauseButton(false); // Reset pause button state
}

function hideGlobalSpeechControls() {
    $('#globalSpeechControls').addClass('d-none');
}

function updateGlobalSpeechControlsText(text) {
    $('#speechControlText').html(`<i class="material-icons me-1" style="font-size: 14px;">volume_up</i>${text}`);
}

function testTextToSpeech() {
    const testText = "This is a test of the text-to-speech functionality. The speech synthesis is working correctly.";
    speakText(testText);
}

function loadInitialData() {
    // Load documents when the app starts
    setTimeout(() => {
        if ($('#documents').hasClass('active')) {
            refreshDocuments();
        }
    }, 500);
}

// Search Functions
async function performSearch() {
    const query = $('#searchQuery').val().trim();
    if (!query) {
        showAlert('searchError', 'Please enter a search query.');
        return;
    }

    showLoading('searchLoading');
    hideElement('searchResults');
    hideElement('searchError');
    hideElement('processingSteps');

    // Update config from UI
    updateSearchConfig();

    const searchRequest = {
        Query: query,                    // Changed from query to Query
        Config: currentSearchConfig,     // Changed from config to Config
        ChatHistory: []                  // Changed from chatHistory to ChatHistory
    };

    try {
        const response = await fetch('/Search/PerformSearch', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': $('input[name="__RequestVerificationToken"]').val()
            },
            body: JSON.stringify(searchRequest)
        });

        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        const data = await response.json();
        displaySearchResults(data);
        displayProcessingSteps(data.processingSteps, 'processingStepsContent');
        showElement('processingSteps');

    } catch (error) {
        console.error('Search error:', error);
        showAlert('searchError', `Search failed: ${error.message}`);
    } finally {
        hideLoading('searchLoading');
    }
}

function updateSearchConfig() {
    currentSearchConfig.UseKnowledgeAgent = $('#useKnowledgeAgent').is(':checked'); // Updated property name
    currentSearchConfig.IncludeImages = $('#includeImages').is(':checked'); // Updated property name
    currentSearchConfig.IncludeText = $('#includeText').is(':checked'); // Updated property name
    currentSearchConfig.Top = parseInt($('#topResults').val()); // Updated property name
    currentSearchConfig.Threshold = parseFloat($('#threshold').val()); // Updated property name
}

function displaySearchResults(data) {
    const container = $('#searchResultsContent');
    container.empty();

    if (!data.results || data.results.length === 0) {
        container.html('<p class="text-muted">No results found.</p>');
        showElement('searchResults');
        return;
    }

    const resultsHtml = data.results.map((result, index) => `
        <div class="card mb-3">
            <div class="card-body">
                <div class="d-flex justify-content-between align-items-start mb-2">
                    <h6 class="card-title mb-0">
                        <span class="badge bg-primary me-2">${index + 1}</span>
                        ${escapeHtml(result.sourcePath || 'Unknown Source')}
                    </h6>
                    <span class="badge bg-secondary">${(result.score * 100).toFixed(1)}%</span>
                </div>
                <p class="card-text">${escapeHtml(result.content).substring(0, 300)}${result.content.length > 300 ? '...' : ''}</p>
                <div class="d-flex justify-content-between align-items-center">
                    <small class="text-muted">Type: ${escapeHtml(result.contentType)}</small>
                    ${result.metadata && Object.keys(result.metadata).length > 0 ? 
                        `<button class="btn btn-sm btn-outline-info" onclick="showMetadata('${result.id}')">
                            <i class="material-icons" style="font-size: 14px;">info</i> Metadata
                        </button>` : ''
                    }
                </div>
            </div>
        </div>
    `).join('');

    container.html(resultsHtml);
    showElement('searchResults');
}

// Chat Functions
async function sendChatMessage() {
    const message = $('#chatInput').val().trim();
    if (!message) return;

    // Add user message to chat
    addChatMessage('user', message);
    $('#chatInput').val('');

    // Hide speech status if showing
    $('#speechStatus').addClass('d-none');

    // Add thinking message to chat
    addThinkingMessage();

    hideElement('chatError');
    hideElement('chatSources');
    hideElement('chatProcessingSteps');

    // Chat config for the request
    const chatConfig = {
        UseKnowledgeAgent: $('#chatUseKnowledgeAgent').is(':checked'),
        IncludeImages: $('#chatIncludeImages').is(':checked'),
        IncludeText: $('#chatIncludeText').is(':checked'),
        Top: parseInt($('#chatTopResults').val()),
        Threshold: parseFloat($('#chatThreshold').val())
    };

    const chatRequest = {
        Message: message,
        ChatHistory: chatHistory,
        SearchConfig: chatConfig,
        RequireSecurityTrimming: $('#chatRequireSecurityTrimming').is(':checked')
    };

    try {
        // Get antiforgery token
        const antiforgeryToken = $('input[name="__RequestVerificationToken"]').val();
        const headers = {
            'Content-Type': 'application/json'
        };
        
        // Only add the token if it exists
        if (antiforgeryToken) {
            headers['RequestVerificationToken'] = antiforgeryToken;
        }

        console.log('?? Sending chat request:', {
            url: '/Chat/SendMessage',
            headers: headers,
            bodyPreview: {
                Message: chatRequest.Message,
                RequireSecurityTrimming: chatRequest.RequireSecurityTrimming,
                SearchConfigKeys: Object.keys(chatRequest.SearchConfig),
                ChatHistoryLength: chatRequest.ChatHistory.length
            }
        });

        const response = await fetch('/Chat/SendMessage', {
            method: 'POST',
            headers: headers,
            body: JSON.stringify(chatRequest)
        });

        console.log('?? Received response:', {
            status: response.status,
            statusText: response.statusText,
            ok: response.ok,
            headers: Object.fromEntries(response.headers.entries())
        });

        if (!response.ok) {
            // Try to parse JSON error response
            let errorMessage = `HTTP error! status: ${response.status}`;
            try {
                const errorData = await response.json();
                console.log('? Error response data:', errorData);
                if (errorData.error) {
                    errorMessage = errorData.error;
                    // Handle authentication errors
                    if (errorData.requiresAuth) {
                        errorMessage += ' Redirecting to sign-in...';
                        setTimeout(() => {
                            window.location.href = '/Account/SignIn';
                        }, 2000);
                    }
                }
            } catch (parseError) {
                console.error('? Failed to parse error response as JSON:', parseError);
                // If JSON parsing fails, the response might be HTML (like a redirect page)
                const responseText = await response.text();
                console.log('?? Error response text (first 500 chars):', responseText.substring(0, 500));
                
                if (responseText.includes('<!DOCTYPE') || responseText.includes('<html')) {
                    errorMessage = 'Authentication required. You may have been logged out. Please refresh the page and sign in again.';
                    // Auto-redirect to sign-in after a delay
                    setTimeout(() => {
                        window.location.href = '/Account/SignIn';
                    }, 3000);
                } else {
                    errorMessage = `Server error: ${response.status} ${response.statusText}`;
                }
            }
            throw new Error(errorMessage);
        }

        const data = await response.json();
        console.log('? Successfully parsed response data:', {
            hasResponse: !!data.response,
            responseLength: data.response?.length || 0,
            sourcesCount: data.sources?.length || 0,
            processingStepsCount: data.processingSteps?.length || 0,
            fullDataKeys: Object.keys(data),
            responseText: data.response ? data.response.substring(0, 100) + '...' : 'NO RESPONSE'
        });
        
        // Remove thinking message
        removeThinkingMessage();
        
        // Check if we have a response
        let responseText = '';
        if (data.response) {
            responseText = data.response;
        } else if (data.Response) { // Check for Pascal case
            responseText = data.Response;
        } else if (data.message) { // Fallback to message
            responseText = data.message;
        } else if (data.Message) { // Pascal case message
            responseText = data.Message;
        } else {
            responseText = 'Sorry, I received an empty response. Please try again.';
            console.warn('No response text found in data:', data);
        }
        
        // Add assistant response to chat
        const messageId = 'msg_' + Date.now();
        addChatMessage('assistant', responseText, messageId);
        
        // Speak the response if text-to-speech is enabled
        if ($('#chatEnableTextToSpeech').is(':checked') && responseText) {
            // Add a slight delay to allow the message to be displayed first
            setTimeout(() => {
                speakText(responseText, messageId);
            }, 500);
        }
        
        // Update chat history
        chatHistory.push({ Role: 'user', Content: message });        // Updated property names
        chatHistory.push({ Role: 'assistant', Content: responseText }); // Updated property names

        // Display sources and processing steps
        if (data.sources && data.sources.length > 0) {
            displayChatSources(data.sources);
            showElement('chatSources');
        } else if (data.Sources && data.Sources.length > 0) {
            displayChatSources(data.Sources);
            showElement('chatSources');
        }

        if (data.processingSteps && data.processingSteps.length > 0) {
            displayProcessingSteps(data.processingSteps, 'chatProcessingStepsContent');
            showElement('chatProcessingSteps');
        } else if (data.ProcessingSteps && data.ProcessingSteps.length > 0) {
            displayProcessingSteps(data.ProcessingSteps, 'chatProcessingStepsContent');
            showElement('chatProcessingSteps');
        }

    } catch (error) {
        console.error('Chat error:', error);
        // Remove thinking message on error
        removeThinkingMessage();
        showAlert('chatError', `Chat failed: ${error.message}`);
    }
}

function addChatMessage(role, content, messageId = null) {
    const messagesContainer = $('#chatMessages');
    
    // Remove welcome message if it exists
    const welcomeMessage = messagesContainer.find('.text-center.text-muted');
    if (welcomeMessage.length > 0) {
        welcomeMessage.remove();
    }

    const messageClass = role === 'user' ? 'bg-primary text-white' : 'bg-light';
    const alignment = role === 'user' ? 'ms-auto' : 'me-auto';
    const icon = role === 'user' ? 'person' : 'smart_toy';

    // Generate unique message ID if not provided
    const msgId = messageId || `msg_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;

    console.log('Adding chat message:', role, 'with ID:', msgId, 'content length:', content?.length || 0, 'speechSynthesis available:', !!speechSynthesis);

    // Handle empty or null content
    if (!content || content.trim() === '') {
        content = role === 'assistant' 
            ? 'I apologize, but I received an empty response. Please try asking your question again.' 
            : 'Empty message';
        console.warn('Empty content provided for message:', msgId, 'using fallback');
    }

    // Add speak button for assistant messages if text-to-speech is available
    const speakButton = role === 'assistant' && speechSynthesis ? 
        `<button class="btn btn-sm btn-outline-secondary speak-btn message-speak-btn" data-message-id="${msgId}" title="Read message aloud" type="button">
            <i class="material-icons" style="font-size: 12px;">volume_up</i>
        </button>` : '';

    const messageHtml = `
        <div class="d-flex mb-3 ${role === 'user' ? 'justify-content-end' : 'justify-content-start'}">
            <div class="card ${messageClass} ${alignment}" style="max-width: 80%; min-width: 200px;" data-message-id="${msgId}">
                <div class="card-body py-2 px-3">
                    <div class="d-flex align-items-center mb-1 justify-content-between">
                        <div class="d-flex align-items-center">
                            <i class="material-icons me-2" style="font-size: 16px;">${icon}</i>
                            <small class="fw-bold">${role === 'user' ? 'You' : 'Assistant'}</small>
                        </div>
                        ${speakButton}
                    </div>
                    <div class="message-content" style="word-wrap: break-word; white-space: pre-wrap;">${role === 'assistant' ? formatMarkdown(content) : escapeHtml(content)}</div>
                </div>
            </div>
        </div>
    `;

    messagesContainer.append(messageHtml);
    
    // Log button creation for debugging
    if (role === 'assistant' && speechSynthesis) {
        console.log('Speak button created for message:', msgId);
        // Verify the button was added
        setTimeout(() => {
            const buttonCheck = $(`.speak-btn[data-message-id="${msgId}"]`);
            const messageCheck = $(`.card[data-message-id="${msgId}"] .message-content`);
            console.log('Message verification - found:', buttonCheck.length, 'button(s) and content length:', messageCheck.text().length, 'for message:', msgId);
        }, 100);
    }
    
    // Scroll to bottom
    messagesContainer.scrollTop(messagesContainer[0].scrollHeight);
}

function handleSpeakButtonClick(messageId, text) {
    console.log('Speak button clicked:', messageId, 'Speaking:', isSpeaking, 'Current ID:', currentSpeakingMessageId);
    
    if (currentSpeakingMessageId === messageId && isSpeaking) {
        // Stop current speech for this message
        console.log('Stopping speech for message:', messageId);
        stopTextToSpeech();
    } else {
        // Start speaking this message
        console.log('Starting speech for message:', messageId);
        
        // Get the clean text content from the message (strip HTML)
        const messageCard = $(`.card[data-message-id="${messageId}"]`);
        const messageContentElement = messageCard.find('.message-content');
        
        // Get text content and clean it up
        let cleanText = messageContentElement.text().trim();
        
        // If text is empty, try to get from original content
        if (!cleanText && text) {
            cleanText = text;
        }
        
        // Remove any extra whitespace and normalize
        cleanText = cleanText.replace(/\s+/g, ' ').trim();
        
        if (cleanText) {
            speakText(cleanText, messageId);
        } else {
            console.warn('No text content found for message:', messageId);
        }
    }
}

function addThinkingMessage() {
    const messagesContainer = $('#chatMessages');
    
    const thinkingHtml = `
        <div class="d-flex mb-3 justify-content-start thinking-message" id="thinking-message">
            <div class="card bg-light me-auto" style="max-width: 80%;">
                <div class="card-body py-2 px-3">
                    <div class="d-flex align-items-center mb-1">
                        <i class="material-icons me-2" style="font-size: 16px;">smart_toy</i>
                        <small class="fw-bold">Assistant</small>
                    </div>
                    <div class="message-content d-flex align-items-center">
                        <div class="spinner-border spinner-border-sm text-primary me-2" role="status" style="width: 16px; height: 16px;">
                            <span class="visually-hidden">Loading...</span>
                        </div>
                        <span class="text-muted">Thinking<span class="thinking-dots"></span></span>
                    </div>
                </div>
            </div>
        </div>
    `;

    messagesContainer.append(thinkingHtml);
    
    // Scroll to bottom
    messagesContainer.scrollTop(messagesContainer[0].scrollHeight);
}

function removeThinkingMessage() {
    $('#thinking-message').remove();
}

function displayChatSources(sources) {
    const container = $('#chatSourcesContent');
    container.empty();

    const sourcesHtml = sources.map((source, index) => `
        <div class="card mb-2">
            <div class="card-body py-2">
                <div class="d-flex justify-content-between align-items-start">
                    <div>
                        <h6 class="mb-1">
                            <span class="badge bg-info me-2">${index + 1}</span>
                            ${escapeHtml(source.sourcePath || 'Unknown Source')}
                        </h6>
                        <p class="mb-1 small">${escapeHtml(source.content).substring(0, 150)}${source.content.length > 150 ? '...' : ''}</p>
                    </div>
                    <span class="badge bg-secondary">${(source.score * 100).toFixed(1)}%</span>
                </div>
            </div>
        </div>
    `).join('');

    container.html(sourcesHtml);
}

function handleChatKeyPress(event) {
    if (event.key === 'Enter' && !event.shiftKey) {
        event.preventDefault();
        sendChatMessage();
    }
}

function clearChat() {
    chatHistory = [];
    const messagesContainer = $('#chatMessages');
    messagesContainer.html(`
        <div class="text-center text-muted py-4">
            <i class="material-icons" style="font-size: 48px;">chat_bubble_outline</i>
            <p class="mt-2">Start a conversation about your documents</p>
        </div>
    `);
    hideElement('chatSources');
    hideElement('chatProcessingSteps');
    hideElement('chatError');
    hideElement('speechStatus');
    
    // Stop any ongoing speech and hide controls
    stopTextToSpeech();
    hideGlobalSpeechControls();
}

// Documents Functions
async function refreshDocuments() {
    showLoading('documentsLoading');
    hideElement('documentsContent');
    hideElement('documentsEmpty');
    hideElement('documentsError');

    try {
        const response = await fetch('/Documents/GetDocuments');
        
        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        const documents = await response.json();
        displayDocuments(documents);
        updateDocumentStatistics(documents);

    } catch (error) {
        console.error('Documents error:', error);
        showAlert('documentsError', `Failed to load documents: ${error.message}`);
    } finally {
        hideLoading('documentsLoading');
    }
}

function displayDocuments(documents) {
    const tableBody = $('#documentsTable');
    tableBody.empty();

    if (!documents || documents.length === 0) {
        showElement('documentsEmpty');
        return;
    }

    const documentsHtml = documents.map(doc => {
        // Use title if available, otherwise fall back to fileName
        const displayName = doc.title && doc.title.trim() ? doc.title : doc.fileName;
        const showFileName = doc.title && doc.title.trim() && doc.title !== doc.fileName;
        
        return `
        <tr>
            <td>
                <div class="d-flex align-items-center">
                    <i class="material-icons me-2 text-muted">${getFileIcon(doc.contentType)}</i>
                    <div>
                        <div>${escapeHtml(displayName)}</div>
                        ${showFileName ? `<small class="text-muted">${escapeHtml(doc.fileName)}</small>` : ''}
                        ${doc.description ? `<small class="text-muted d-block">${escapeHtml(doc.description)}</small>` : ''}

                    </div>
                </div>
            </td>
            <td><span class="badge bg-light text-dark">${escapeHtml(doc.contentType)}</span></td>
            <td>${formatFileSize(doc.size || doc.fileSize)}</td>
            <td><small>${formatDate(doc.uploadedAt || doc.createdAt)}</small></td>
            <td><span class="badge ${getStatusBadgeClass(doc.status)}">${escapeHtml(doc.status)}</span></td>
            <td>
                <button class="btn btn-sm btn-outline-danger" onclick="deleteDocument('${doc.id}', '${escapeHtml(displayName)}')">
                    <i class="material-icons" style="font-size: 14px;">delete</i>
                </button>
            </td>
        </tr>
    `;
    }).join('');

//    const documentsHtml = documents.map(doc => `
//        <tr>
//            <td>${escapeHtml(doc.title || doc.fileName)}</td>
//            <td><span class="badge bg-light text-dark">${escapeHtml(doc.contentType)}</span></td>
//            <td>${formatFileSize(doc.size || doc.fileSize)}</td>
//            <td><small>${formatDate(doc.uploadedAt || doc.createdAt)}</small></td>
//            <td><span class="badge ${getStatusBadgeClass(doc.status)}">${escapeHtml(doc.status)}</span></td>
//            <td>
//                <button class="btn btn-sm btn-outline-danger" onclick="deleteDocument('${doc.id}', '${escapeHtml(doc.title || doc.fileName)}')">
//                    <i class="material-icons" style="font-size: 14px;">delete</i>
//                </button>
//            </td>
//        </tr>
//    `).join('');

    tableBody.html(documentsHtml);
    showElement('documentsContent');
}

async function uploadDocument() {
    const fileInput = $('#documentFile')[0];
    if (!fileInput.files.length) {
        showAlert('documentsError', 'Please select files to upload.');
        return;
    }

    const title = $('#documentTitle').val().trim();
    const description = $('#documentDescription').val().trim();

    const formData = new FormData();
    for (let i = 0; i < fileInput.files.length; i++) {
        formData.append('file', fileInput.files[i]);
    }
    
    // Add title and description if provided
    if (title) {
        formData.append('title', title);
    }
    if (description) {
        formData.append('description', description);
    }

    showElement('uploadProgress');
    updateUploadProgress(0, 'Starting upload...');

    try {
        const response = await fetch('/Documents/UploadDocument', {
            method: 'POST',
            body: formData
        });

        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        const result = await response.json();
        
        if (result.success) {
            showAlert('documentsSuccess', 'Documents uploaded successfully!');
            // Clear all form fields
            fileInput.value = '';
            $('#documentTitle').val('');
            $('#documentDescription').val('');
            setTimeout(() => refreshDocuments(), 1000);
        } else {
            throw new Error(result.message || 'Upload failed');
        }

    } catch (error) {
        console.error('Upload error:', error);
        showAlert('documentsError', `Upload failed: ${error.message}`);
    } finally {
        hideElement('uploadProgress');
    }
}

async function deleteDocument(documentId, fileName) {
    if (!confirm(`Are you sure you want to delete "${fileName}"?`)) {
        return;
    }

    try {
        const response = await fetch(`/Documents/DeleteDocument?documentId=${encodeURIComponent(documentId)}`, {
            method: 'DELETE'
        });

        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        showAlert('documentsSuccess', `Document "${fileName}" deleted successfully!`);
        refreshDocuments();

    } catch (error) {
        console.error('Delete error:', error);
        showAlert('documentsError', `Failed to delete document: ${error.message}`);
    }
}

// Index Management Functions
async function refreshIndexes() {
    showLoading('indexesLoading');
    hideElement('indexesContent');
    hideElement('indexesEmpty');
    hideElement('indexesError');

    try {
        const response = await fetch('/IndexManagement/GetIndexes');
        
        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        const indexes = await response.json();
        displayIndexes(indexes);
        updateIndexStatistics(indexes);

    } catch (error) {
        console.error('Indexes error:', error);
        showAlert('indexesError', `Failed to load indexes: ${error.message}`);
    } finally {
        hideLoading('indexesLoading');
    }
}

function displayIndexes(indexes) {
    const tableBody = $('#indexesTable');
    tableBody.empty();

    if (!indexes || indexes.length === 0) {
        showElement('indexesEmpty');
        return;
    }

    const indexesHtml = indexes.map(index => `
        <tr>
            <td>
                <div class="d-flex align-items-center">
                    <i class="material-icons me-2 text-muted">storage</i>
                    ${escapeHtml(index.name)}
                </div>
            </td>
            <td>${escapeHtml(index.description || '-')}</td>
            <td><span class="badge bg-info">${index.documentCount}</span></td>
            <td><small>${formatDate(index.createdAt)}</small></td>
            <td><span class="badge ${getStatusBadgeClass(index.status)}">${escapeHtml(index.status)}</span></td>
            <td>
                <button class="btn btn-sm btn-outline-danger" onclick="deleteIndex('${index.name}')">
                    <i class="material-icons" style="font-size: 14px;">delete</i>
                </button>
            </td>
        </tr>
    `).join('');

    tableBody.html(indexesHtml);
    showElement('indexesContent');
}

async function createIndex() {
    const name = $('#indexName').val().trim();
    const description = $('#indexDescription').val().trim();

    if (!name) {
        showAlert('indexesError', 'Please enter an index name.');
        return;
    }

    const createRequest = {
        name: name,
        description: description,
        configuration: {}
    };

    try {
        const response = await fetch('/IndexManagement/CreateIndex', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(createRequest)
        });

        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        const result = await response.json();
        showAlert('indexesSuccess', `Index "${name}" created successfully!`);
        
        // Clear form
        $('#indexName').val('');
        $('#indexDescription').val('');
        
        // Refresh indexes list
        refreshIndexes();

    } catch (error) {
        console.error('Create index error:', error);
        showAlert('indexesError', `Failed to create index: ${error.message}`);
    }
}

async function deleteIndex(indexName) {
    if (!confirm(`Are you sure you want to delete the index "${indexName}"?`)) {
        return;
    }

    try {
        const response = await fetch(`/IndexManagement/DeleteIndex?indexName=${encodeURIComponent(indexName)}`, {
            method: 'DELETE'
        });

        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        showAlert('indexesSuccess', `Index "${indexName}" deleted successfully!`);
        refreshIndexes();

    } catch (error) {
        console.error('Delete index error:', error);
        showAlert('indexesError', `Failed to delete index: ${error.message}`);
    }
}

// Utility Functions
function showElement(elementId) {
    $(`#${elementId}`).removeClass('d-none');
}

function hideElement(elementId) {
    $(`#${elementId}`).addClass('d-none');
}

function showLoading(elementId) {
    showElement(elementId);
}

function hideLoading(elementId) {
    hideElement(elementId);
}

function showAlert(elementId, message) {
    const alertElement = $(`#${elementId}`);
    const messageElement = $(`#${elementId}Message`);
    
    if (messageElement.length) {
        messageElement.text(message);
    } else {
        alertElement.text(message);
    }
    
    showElement(elementId);
    
    // Auto-hide success alerts after 5 seconds
    if (elementId.includes('Success')) {
        setTimeout(() => hideElement(elementId), 5000);
    }
}

function escapeHtml(text) {
    if (!text) return '';
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

function formatFileSize(bytes) {
    if (!bytes) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
}

function formatDate(dateString) {
    if (!dateString) return '';
    const date = new Date(dateString);
    return date.toLocaleString();
}

function getFileIcon(contentType) {
    if (!contentType) return 'description';
    
    if (contentType.includes('pdf')) return 'picture_as_pdf';
    if (contentType.includes('image')) return 'image';
    if (contentType.includes('word') || contentType.includes('document')) return 'description';
    if (contentType.includes('text')) return 'text_snippet';
    
    return 'description';
}

function getStatusBadgeClass(status) {
    if (!status) return 'bg-secondary';
    
    const statusLower = status.toLowerCase();
    if (statusLower.includes('success') || statusLower.includes('complete') || statusLower.includes('ready')) {
        return 'bg-success';
    }
    if (statusLower.includes('error') || statusLower.includes('failed')) {
        return 'bg-danger';
    }
    if (statusLower.includes('processing') || statusLower.includes('pending')) {
        return 'bg-warning';
    }
    
    return 'bg-secondary';
}

function formatMarkdown(text) {
    if (!text) return '';
    
    // Simple markdown formatting
    return text
        .replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>')
        .replace(/\*(.*?)\*/g, '<em>$1</em>')
        .replace(/`(.*?)`/g, '<code>$1</code>')
        .replace(/\n/g, '<br>');
}

function displayProcessingSteps(steps, containerId) {
    if (!steps || !steps.length) return;
    
    const container = $(`#${containerId}`);
    container.empty();

    const stepsHtml = steps.map((step, index) => `

        <div class="d-flex align-items-start mb-2">
            <div class="badge bg-primary me-2 mt-1" style="min-width: 24px;">${index + 1}</div>
            <div class="flex-grow-1">
                <h6 class="mb-1">${escapeHtml(step.title)}</h6>
                ${step.description ? `<small class="text-muted">${escapeHtml(step.description)}</small>` : ''}
                ${step.timestamp ? `<div><small class="text-muted">${formatDate(step.timestamp)}</small></div>` : ''}
            </div>
        </div>
    `).join('');

    container.html(stepsHtml);
}

function updateUploadProgress(percent, message) {
    $('#uploadProgressBar').css('width', `${percent}%`);
    $('#uploadProgressText').text(message);
}

function updateDocumentStatistics(documents) {
    if (!documents) return;
    
    const totalDocs = documents.length;
    const totalSize = documents.reduce((sum, doc) => sum + (doc.size || 0), 0);
    
    $('#totalDocuments').text(totalDocs);
    $('#totalSize').text(formatFileSize(totalSize));
    
    // Update document types breakdown
    const typeCount = {};
    documents.forEach(doc => {
        const type = doc.contentType || 'Unknown';
        typeCount[type] = (typeCount[type] || 0) + 1;
    });
    
    const typesHtml = Object.entries(typeCount).map(([type, count]) => `
        <div class="d-flex justify-content-between align-items-center mb-1">
            <span class="small">${escapeHtml(type)}</span>
            <span class="badge bg-light text-dark">${count}</span>
        </div>
    `).join('');
    
    $('#documentTypes').html(typesHtml);
}

function updateIndexStatistics(indexes) {
    if (!indexes) return;
    
    const totalIndexes = indexes.length;
    const totalDocs = indexes.reduce((sum, index) => sum + (index.documentCount || 0), 0);
    
    $('#totalIndexes').text(totalIndexes);
    $('#totalIndexedDocuments').text(totalDocs);
}

// Debug functions - available in browser console for testing
function testSpeechButton() {
    console.log('=== Speech Button Test ===');
    
    // Check if speech synthesis is available
    console.log('Speech synthesis available:', !!window.speechSynthesis);
    
    // Check for speak buttons
    const speakButtons = $('.speak-btn');
    console.log('Found speak buttons:', speakButtons.length);
    
    speakButtons.each(function(index) {
        const messageId = $(this).data('message-id');
        console.log(`Button ${index + 1}: messageId = ${messageId}`);
    });
    
    // Test clicking the first button if available
    if (speakButtons.length > 0) {
        console.log('Testing click on first button...');
        speakButtons.first().trigger('click');
    } else {
        console.log('No speak buttons found - try sending a chat message first');
    }
    
    console.log('Current speaking state:', {
        isSpeaking: isSpeaking,
        currentSpeakingMessageId: currentSpeakingMessageId,
        speechSynthesisSupported: !!window.speechSynthesis
    });
}

function testSpeechSynthesis() {
    console.log('=== Speech Synthesis Test ===');
    
    if (!window.speechSynthesis) {
        console.error('Speech synthesis not supported');
        return;
    }
    
    const testText = "This is a test of the speech synthesis functionality.";
    console.log('Testing speech synthesis with text:', testText);
    
    speakText(testText, 'test-message-id');
}

function testChatMessage() {
    console.log('=== Chat Message Test ===');
    
    // Test adding a chat message directly
    const testContent = "This is a test assistant message to check if the layout is working correctly.";
    const testId = 'test-' + Date.now();
    
    console.log('Adding test message with content:', testContent);
    addChatMessage('assistant', testContent, testId);
    
    // Check if the message was added correctly
    setTimeout(() => {
        const messageElement = $(`.card[data-message-id="${testId}"]`);
        const contentElement = messageElement.find('.message-content');
        
        console.log('Test message verification:');
        console.log('- Message element found:', messageElement.length > 0);
        console.log('- Content element found:', contentElement.length > 0);
        console.log('- Content text:', contentElement.text());
        console.log('- Message HTML:', messageElement.length > 0 ? messageElement[0].outerHTML.substring(0, 200) + '...' : 'Not found');
    }, 500);
}

function debugLastResponse() {
    console.log('=== Last Response Debug ===');
    
    // Check the last few console logs for response data
    console.log('Chat history length:', chatHistory.length);
    if (chatHistory.length > 0) {
        console.log('Last user message:', chatHistory[chatHistory.length - 2]);
        console.log('Last assistant message:', chatHistory[chatHistory.length - 1]);
    }
    
    // Check for assistant messages in the DOM
    const assistantMessages = $('.card[data-message-id]').filter(function() {
        return $(this).find('.message-content').length > 0 && 
               $(this).find('i.material-icons').text() === 'smart_toy';
    });
    
    console.log('Assistant messages in DOM:', assistantMessages.length);
    assistantMessages.each(function(index) {
        const messageId = $(this).data('message-id');
        const content = $(this).find('.message-content').text();
        console.log(`Assistant message ${index + 1}: ID=${messageId}, content length=${content.length}, content="${content.substring(0, 100)}..."`);
    });
}

// Make test functions globally available
window.testSpeechButton = testSpeechButton;
window.testSpeechSynthesis = testSpeechSynthesis;
window.testChatMessage = testChatMessage;
window.debugLastResponse = debugLastResponse;
