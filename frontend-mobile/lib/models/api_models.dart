// ─── Auth DTOs ─────────────────────────────────────────────────────────────

class LoginRequest {
  final String email;
  final String password;
  LoginRequest({required this.email, required this.password});
  Map<String, dynamic> toJson() => {'email': email, 'password': password};
}

class RegisterRequest {
  final String firstName;
  final String lastName;
  final String email;
  final String password;
  RegisterRequest({
    required this.firstName,
    required this.lastName,
    required this.email,
    required this.password,
  });
  Map<String, dynamic> toJson() => {
        'firstName': firstName,
        'lastName': lastName,
        'email': email,
        'password': password,
      };
}

// ─── Procurement Request DTOs ──────────────────────────────────────────────

class ProcurementRequestItemInput {
  String itemName;
  String description;
  int quantity;
  String unit;
  double estimatedUnitPrice;

  ProcurementRequestItemInput({
    this.itemName = '',
    this.description = '',
    this.quantity = 1,
    this.unit = '',
    this.estimatedUnitPrice = 0.0,
  });

  Map<String, dynamic> toJson() => {
        'itemName': itemName,
        'description': description,
        'quantity': quantity,
        'unit': unit,
        'estimatedUnitPrice': estimatedUnitPrice,
      };
}

class CreateProcurementRequestDto {
  final String title;
  final String description;
  final String justification;
  final String priority;
  final String requiredByDate; // ISO 8601
  final List<ProcurementRequestItemInput> items;

  CreateProcurementRequestDto({
    required this.title,
    required this.description,
    required this.justification,
    required this.priority,
    required this.requiredByDate,
    required this.items,
  });

  Map<String, dynamic> toJson() => {
        'title': title,
        'description': description,
        'justification': justification,
        'priority': priority,
        'requiredByDate': requiredByDate,
        'items': items.map((i) => i.toJson()).toList(),
      };
}

class ProcurementRequestItemResponse {
  final String id;
  final String itemName;
  final String description;
  final int quantity;
  final String unit;
  final double estimatedUnitPrice;

  ProcurementRequestItemResponse.fromJson(Map<String, dynamic> j)
      : id = j['id'] as String,
        itemName = j['itemName'] as String,
        description = j['description'] as String? ?? '',
        quantity = j['quantity'] as int,
        unit = j['unit'] as String? ?? '',
        estimatedUnitPrice = (j['estimatedUnitPrice'] as num).toDouble();
}

class ProcurementRequestResponse {
  final String id;
  final String requestNumber;
  final String requesterId;
  final String title;
  final String description;
  final String justification;
  final String priority;
  final String requiredByDate;
  final double estimatedTotal;
  final String status;
  final String createdAt;
  final String updatedAt;
  final List<ProcurementRequestItemResponse> items;

  ProcurementRequestResponse.fromJson(Map<String, dynamic> j)
      : id = j['id'] as String,
        requestNumber = j['requestNumber'] as String,
        requesterId = j['requesterId'] as String,
        title = j['title'] as String,
        description = j['description'] as String? ?? '',
        justification = j['justification'] as String? ?? '',
        priority = j['priority'] as String,
        requiredByDate = j['requiredByDate'] as String,
        estimatedTotal = (j['estimatedTotal'] as num).toDouble(),
        status = j['status'] as String,
        createdAt = j['createdAt'] as String,
        updatedAt = j['updatedAt'] as String,
        items = (j['items'] as List<dynamic>)
            .map((e) => ProcurementRequestItemResponse.fromJson(e as Map<String, dynamic>))
            .toList();
}

// ─── AI Workflow DTOs ──────────────────────────────────────────────────────

class ProcessAiRequest {
  final String objective;
  final String? existingRequestId;
  final String? workflowId;

  ProcessAiRequest({
    required this.objective,
    this.existingRequestId,
    this.workflowId,
  });

  Map<String, dynamic> toJson() => {
        'objective': objective,
        if (existingRequestId != null && existingRequestId!.isNotEmpty)
          'existingRequestId': existingRequestId,
        if (workflowId != null && workflowId!.isNotEmpty)
          'workflowId': workflowId,
      };
}

class WorkflowStep {
  final int stepNumber;
  final String stage;
  final String agentName;
  final String objective;
  final String status;
  final String? outcomeSummary;

  WorkflowStep.fromJson(Map<String, dynamic> j)
      : stepNumber = j['stepNumber'] as int? ?? 0,
        stage = j['stage'] as String? ?? '',
        agentName = j['agentName'] as String? ?? '',
        objective = j['objective'] as String? ?? '',
        status = j['status'] as String? ?? '',
        outcomeSummary = j['outcomeSummary'] as String?;
}

class WorkflowPlan {
  final List<WorkflowStep> steps;
  WorkflowPlan({this.steps = const []});

  WorkflowPlan.fromJson(Map<String, dynamic>? j)
      : steps = (j?['steps'] as List<dynamic>?)
            ?.map((e) => WorkflowStep.fromJson(e as Map<String, dynamic>))
            .toList() ?? [];
}

class WorkflowAuditEntry {
  final String id;
  final String timestamp;
  final String stage;
  final String actor;
  final String action;
  final String? toolName;
  final String status;
  final String details;
  final bool isSecurityViolation;

  WorkflowAuditEntry.fromJson(Map<String, dynamic> j)
      : id = j['id'] as String? ?? '',
        timestamp = j['timestamp'] as String? ?? '',
        stage = j['stage'] as String? ?? '',
        actor = j['actor'] as String? ?? '',
        action = j['action'] as String? ?? '',
        toolName = j['toolName'] as String?,
        status = j['status'] as String? ?? '',
        details = j['details'] as String? ?? '',
        isSecurityViolation = j['isSecurityViolation'] as bool? ?? false;
}

class WorkflowProcessResponse {
  final String workflowId;
  final String status;
  final String currentStage;
  final String? procurementRequestId;
  final String? requestNumber;
  final double? estimatedTotal;
  final String? executionSummary;
  final String? clarificationPrompt;
  final List<String> errors;
  final WorkflowPlan plan;
  final List<WorkflowAuditEntry> auditTrail;
  final String updatedAt;

  WorkflowProcessResponse.fromJson(Map<String, dynamic> j)
      : workflowId = j['workflowId'] as String? ?? '',
        status = j['status'] as String? ?? '',
        currentStage = j['currentStage'] as String? ?? '',
        procurementRequestId = j['procurementRequestId'] as String?,
        requestNumber = j['requestNumber'] as String?,
        estimatedTotal = (j['estimatedTotal'] as num?)?.toDouble(),
        executionSummary = j['executionSummary'] as String?,
        clarificationPrompt = j['clarificationPrompt'] as String?,
        errors = (j['errors'] as List<dynamic>?)?.map((e) => e.toString()).toList() ?? [],
        plan = j['plan'] != null
            ? WorkflowPlan.fromJson(j['plan'] as Map<String, dynamic>)
            : WorkflowPlan(),
        auditTrail = (j['auditTrail'] as List<dynamic>?)
            ?.map((e) => WorkflowAuditEntry.fromJson(e as Map<String, dynamic>))
            .toList() ?? [],
        updatedAt = j['updatedAt'] as String? ?? '';
}

// ─── Approval Workflow DTOs ────────────────────────────────────────────────

class ApprovalDecisionResponse {
  final String id;
  final String managerId;
  final String managerName;
  final String decision;
  final String? comments;
  final String createdAt;

  ApprovalDecisionResponse.fromJson(Map<String, dynamic> j)
      : id = j['id'] as String? ?? '',
        managerId = j['managerId'] as String? ?? '',
        managerName = j['managerName'] as String? ?? '',
        decision = j['decision'] as String? ?? '',
        comments = j['comments'] as String?,
        createdAt = j['createdAt'] as String? ?? '';
}

class NotificationResponse {
  final String id;
  final String recipientUserId;
  final String title;
  final String message;
  final String type;
  final bool isRead;
  final String createdAt;

  NotificationResponse.fromJson(Map<String, dynamic> j)
      : id = j['id'] as String? ?? '',
        recipientUserId = j['recipientUserId'] as String? ?? '',
        title = j['title'] as String? ?? '',
        message = j['message'] as String? ?? '',
        type = j['type'] as String? ?? '',
        isRead = j['isRead'] as bool? ?? false,
        createdAt = j['createdAt'] as String? ?? '';
}

class VendorEvaluationItemResponse {
  final String id;
  final String vendorId;
  final int rank;
  final double overallScore;
  final String reasoning;
  final List<String> riskFlags;

  VendorEvaluationItemResponse.fromJson(Map<String, dynamic> j)
      : id = j['id'] as String? ?? '',
        vendorId = j['vendorId'] as String? ?? '',
        rank = j['rank'] as int? ?? 0,
        overallScore = (j['overallScore'] as num?)?.toDouble() ?? 0.0,
        reasoning = j['reasoning'] as String? ?? '',
        riskFlags = (j['riskFlags'] as List<dynamic>?)?.map((e) => e.toString()).toList() ?? [];
}

class ProcurementEvaluationSummaryResponse {
  final String procurementRequestId;
  final int totalCandidatesEvaluated;
  final String? topRecommendedVendorId;
  final double? topScore;
  final String recommendationSummary;
  final String evaluatedAt;
  final List<VendorEvaluationItemResponse> rankedEvaluations;

  ProcurementEvaluationSummaryResponse.fromJson(Map<String, dynamic> j)
      : procurementRequestId = j['procurementRequestId'] as String? ?? '',
        totalCandidatesEvaluated = j['totalCandidatesEvaluated'] as int? ?? 0,
        topRecommendedVendorId = j['topRecommendedVendorId'] as String?,
        topScore = (j['topScore'] as num?)?.toDouble(),
        recommendationSummary = j['recommendationSummary'] as String? ?? '',
        evaluatedAt = j['evaluatedAt'] as String? ?? '',
        rankedEvaluations = (j['rankedEvaluations'] as List<dynamic>?)
                ?.map((e) => VendorEvaluationItemResponse.fromJson(e as Map<String, dynamic>))
                .toList() ??
            [];
}

class ApprovalWorkflowResponse {
  final String id;
  final String procurementRequestId;
  final String requestNumber;
  final String requestTitle;
  final double estimatedTotal;
  final String requesterId;
  final String requesterName;
  final String currentStatus;
  final String createdAt;
  final String updatedAt;
  final String? completedAt;
  final List<ApprovalDecisionResponse> decisions;
  final List<NotificationResponse> notifications;
  final ProcurementEvaluationSummaryResponse? vendorRecommendationSummary;

  ApprovalWorkflowResponse.fromJson(Map<String, dynamic> j)
      : id = j['id'] as String? ?? '',
        procurementRequestId = j['procurementRequestId'] as String? ?? '',
        requestNumber = j['requestNumber'] as String? ?? '',
        requestTitle = j['requestTitle'] as String? ?? '',
        estimatedTotal = (j['estimatedTotal'] as num?)?.toDouble() ?? 0.0,
        requesterId = j['requesterId'] as String? ?? '',
        requesterName = j['requesterName'] as String? ?? '',
        currentStatus = j['currentStatus'] as String? ?? '',
        createdAt = j['createdAt'] as String? ?? '',
        updatedAt = j['updatedAt'] as String? ?? '',
        completedAt = j['completedAt'] as String?,
        decisions = (j['decisions'] as List<dynamic>?)
                ?.map((e) => ApprovalDecisionResponse.fromJson(e as Map<String, dynamic>))
                .toList() ??
            [],
        notifications = (j['notifications'] as List<dynamic>?)
                ?.map((e) => NotificationResponse.fromJson(e as Map<String, dynamic>))
                .toList() ??
            [],
        vendorRecommendationSummary = j['vendorRecommendationSummary'] != null
            ? ProcurementEvaluationSummaryResponse.fromJson(
                j['vendorRecommendationSummary'] as Map<String, dynamic>)
            : null;
}

class AIAgentExecutionItem {
  final String id;
  final String agentName;
  final int executionOrder;
  final String executionStatus;
  final String? outputSummary;
  final String? toolExecutionMetadata;
  final String startedAt;

  AIAgentExecutionItem.fromJson(Map<String, dynamic> j)
      : id = j['id'] as String? ?? '',
        agentName = j['agentName'] as String? ?? '',
        executionOrder = j['executionOrder'] as int? ?? 0,
        executionStatus = j['executionStatus'] as String? ?? '',
        outputSummary = j['outputSummary'] as String?,
        toolExecutionMetadata = j['toolExecutionMetadata'] as String?,
        startedAt = j['startedAt'] as String? ?? '';
}

class WorkflowAuditTrailResponse {
  final String workflowId;
  final String procurementRequestId;
  final String requestNumber;
  final String requestTitle;
  final double estimatedTotal;
  final String requesterId;
  final String requesterName;
  final String currentStatus;
  final List<ApprovalDecisionResponse> decisions;
  final List<AIAgentExecutionItem> agentExecutions;
  final List<NotificationResponse> notifications;

  WorkflowAuditTrailResponse.fromJson(Map<String, dynamic> j)
      : workflowId = j['workflowId'] as String? ?? '',
        procurementRequestId = j['procurementRequestId'] as String? ?? '',
        requestNumber = j['requestNumber'] as String? ?? '',
        requestTitle = j['requestTitle'] as String? ?? '',
        estimatedTotal = (j['estimatedTotal'] as num?)?.toDouble() ?? 0.0,
        requesterId = j['requesterId'] as String? ?? '',
        requesterName = j['requesterName'] as String? ?? '',
        currentStatus = j['currentStatus'] as String? ?? '',
        decisions = (j['decisions'] as List<dynamic>?)
                ?.map((e) => ApprovalDecisionResponse.fromJson(e as Map<String, dynamic>))
                .toList() ??
            [],
        agentExecutions = (j['agentExecutions'] as List<dynamic>?)
                ?.map((e) => AIAgentExecutionItem.fromJson(e as Map<String, dynamic>))
                .toList() ??
            [],
        notifications = (j['notifications'] as List<dynamic>?)
                ?.map((e) => NotificationResponse.fromJson(e as Map<String, dynamic>))
                .toList() ??
            [];
}
