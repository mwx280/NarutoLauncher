#pragma once

#include <QFrame>
#include <QVector>

#include "account.h"

class QCheckBox;
class QLineEdit;
class QVBoxLayout;
class QScrollArea;
class QPushButton;
class QLabel;
class QWidget;

// 单个账号卡片：展示备注/账号/密码，支持编辑保存、删除与开始游戏。
class AccountCard : public QFrame {
    Q_OBJECT
public:
    explicit AccountCard(const AccountInfo& info, QWidget* parent = nullptr);

    AccountInfo Info() const;
    void SetInfo(const AccountInfo& info);

signals:
    void StartRequested(const AccountInfo& info);  // 点击「开始游戏」
    void Edited();                                  // 编辑保存后
    void DeleteRequested();                         // 点击删除

private:
    QLineEdit* remark_;
    QLineEdit* username_;
    QLineEdit* password_;
    QLabel* typeLabel_;       // 登录类型标签（"扫码"/"QQ"）
    QCheckBox* scanLogin_;    // 扫码登录（仅编辑时可见）
    QPushButton* editBtn_;    // 编辑/保存 按钮
    QPushButton* startBtn_;   // 开始游戏 按钮（编辑时隐藏）
    bool editing_ = false;

    // 根据登录类型返回卡片主显示名：
    //   扫码登录 → 备注（必填）；QQ 登录 → 有备注显示备注，否则显示 QQ 号。
    QString DisplayName(const AccountInfo& info) const;
    void ApplyMode();
};

// 左侧导航栏：内置账号管理（列表 + 添加表单），整体可收缩/展开。
class SideNav : public QFrame {
    Q_OBJECT
public:
    explicit SideNav(QWidget* parent = nullptr);

    // 从持久化存储加载账号到列表（启动时调用一次）。
    void LoadFromStore(const QVector<AccountInfo>& accounts);

    // 当前全部账号（供外部保存时调用）。
    QVector<AccountInfo> Accounts() const;

    // 是否为收缩状态。
    bool IsCollapsed() const { return collapsed_; }

    // 切换收缩状态（供外部按需调用，内部按钮亦触发）。
    void SetCollapsed(bool collapsed);

signals:
    void StartRequested(const AccountInfo& info);  // 账号开始游戏请求
    void CollapseChanged(bool collapsed);           // 收缩状态变化

private:
    bool collapsed_ = false;
    QPushButton* collapseBtn_;
    QLabel* titleLabel_;
    QWidget* body_;
    QVBoxLayout* accountsLayout_;
    QScrollArea* scroll_;
    QLineEdit* addRemark_;
    QLineEdit* addUsername_;
    QLineEdit* addPassword_;
    QCheckBox* addScanLogin_;   // 添加表单：扫码登录复选框
    QWidget* addForm_;
    QLabel* emptyLabel_;   // 空账号列表时的引导提示

    void AddAccountToUi(const AccountInfo& info);
    void SaveAll();
};
