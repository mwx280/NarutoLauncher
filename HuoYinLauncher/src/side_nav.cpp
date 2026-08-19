#include "side_nav.h"

#include <QCheckBox>
#include <QHBoxLayout>
#include <QLabel>
#include <QLineEdit>
#include <QPushButton>
#include <QScrollArea>
#include <QSignalBlocker>
#include <QVBoxLayout>

namespace {

// 展开 / 收缩时的导航栏宽度（像素）。
const int kExpandedWidth = 270;
const int kCollapsedWidth = 42;

}  // namespace

// ------------------------- 账号卡片 -------------------------

AccountCard::AccountCard(const AccountInfo& info, QWidget* parent) : QFrame(parent) {
    setObjectName("AccountCard");

    QVBoxLayout* lay = new QVBoxLayout(this);
    lay->setContentsMargins(10, 8, 10, 8);
    lay->setSpacing(5);

    // 第一行：登录类型标签 + 主显示名（备注 / QQ号）+ 删除
    QHBoxLayout* top = new QHBoxLayout;
    typeLabel_ = new QLabel(this);
    typeLabel_->setObjectName("CardTypeLabel");
    typeLabel_->setAlignment(Qt::AlignCenter);
    top->addWidget(typeLabel_);
    remark_ = new QLineEdit(info.remark, this);
    remark_->setPlaceholderText("备注");
    remark_->setObjectName("CardRemark");
    top->addWidget(remark_, 1);
    QPushButton* del = new QPushButton("✕", this);
    del->setObjectName("CardDelete");
    del->setFixedSize(20, 20);
    del->setToolTip("删除该账号");
    top->addWidget(del);
    lay->addLayout(top);

    // 第二行：账号 + 密码（扫码登录时隐藏）
    QHBoxLayout* mid = new QHBoxLayout;
    username_ = new QLineEdit(info.username, this);
    username_->setPlaceholderText("账号 / QQ号");
    mid->addWidget(username_, 1);
    password_ = new QLineEdit(info.password, this);
    password_->setPlaceholderText("密码");
    password_->setEchoMode(QLineEdit::Password);
    mid->addWidget(password_, 1);
    lay->addLayout(mid);

    // 第三行：扫码登录（仅编辑时显示）+ 编辑 + 开始游戏
    QHBoxLayout* ops = new QHBoxLayout;
    scanLogin_ = new QCheckBox("扫码登录", this);
    ops->addWidget(scanLogin_);
    ops->addStretch();
    editBtn_ = new QPushButton("编辑", this);
    editBtn_->setObjectName("CardButton");
    ops->addWidget(editBtn_);
    startBtn_ = new QPushButton("开始游戏", this);
    startBtn_->setObjectName("CardButtonPrimary");
    ops->addWidget(startBtn_);
    lay->addLayout(ops);

    SetInfo(info);

    connect(del, &QPushButton::clicked, this, &AccountCard::DeleteRequested);
    connect(editBtn_, &QPushButton::clicked, this, [this]() {
        editing_ = !editing_;
        ApplyMode();
        if (!editing_)
            emit Edited();
    });
    connect(startBtn_, &QPushButton::clicked, this, [this]() {
        emit StartRequested(Info());
    });
    // 编辑时切换登录方式：即时显示/隐藏 QQ 输入框；
    // 从 QQ 切到扫码时，备注若为空则默认填入已有 QQ 号。
    connect(scanLogin_, &QCheckBox::toggled, this, [this](bool checked) {
        username_->setVisible(!checked);
        password_->setVisible(!checked);
        typeLabel_->setText(checked ? "扫码" : "QQ");
        if (checked && remark_->text().trimmed().isEmpty())
            remark_->setText(username_->text());  // QQ 号作为默认备注
    });
}

QString AccountCard::DisplayName(const AccountInfo& info) const {
    if (info.scanLogin)
        return info.remark.isEmpty() ? "扫码登录" : info.remark;
    return info.remark.isEmpty() ? info.username : info.remark;
}

void AccountCard::ApplyMode() {
    const bool editable = editing_;
    const bool scan = scanLogin_->isChecked();

    // 字段可编辑性
    remark_->setReadOnly(!editable);
    username_->setReadOnly(!editable);
    password_->setReadOnly(!editable);
    scanLogin_->setVisible(editable);
    scanLogin_->setEnabled(editable);

    // 编辑时：隐藏「开始游戏」，按钮文字切换 编辑/保存
    startBtn_->setVisible(!editable);
    editBtn_->setText(editable ? "保存" : "编辑");

    // 可见性
    username_->setVisible(editable && !scan);
    password_->setVisible(editable && !scan);
    typeLabel_->setVisible(true);
    remark_->setVisible(true);

    if (!editable) {
        // 非编辑态：主显示名 + 隐藏账号/密码
        remark_->setText(DisplayName(Info()));
        password_->setEchoMode(QLineEdit::Password);
    } else {
        // 编辑态：显示真实备注，扫码账号密码框自动隐藏（由 toggled 处理）
        remark_->setText(Info().remark);
    }
}

AccountInfo AccountCard::Info() const {
    AccountInfo i;
    i.remark = remark_->text();
    i.username = username_->text();
    i.password = password_->text();
    i.scanLogin = scanLogin_->isChecked();
    return i;
}

void AccountCard::SetInfo(const AccountInfo& info) {
    // 先清空触发一次切换逻辑，避免 toggled 干扰初始设置
    QSignalBlocker blocker(scanLogin_);
    scanLogin_->setChecked(info.scanLogin);
    blocker.unblock();
    remark_->setText(info.remark);
    username_->setText(info.username);
    password_->setText(info.password);
    typeLabel_->setText(info.scanLogin ? "扫码" : "QQ");
    ApplyMode();
}

// ------------------------- 左侧导航栏 -------------------------

SideNav::SideNav(QWidget* parent) : QFrame(parent) {
    setObjectName("SideNav");
    setFixedWidth(kExpandedWidth);

    QVBoxLayout* lay = new QVBoxLayout(this);
    lay->setContentsMargins(14, 14, 14, 10);
    lay->setSpacing(10);

    // 顶部：品牌区 + 收缩按钮
    QHBoxLayout* head = new QHBoxLayout;
    QLabel* logo = new QLabel("火", this);
    logo->setObjectName("SideLogo");
    head->addWidget(logo);
    titleLabel_ = new QLabel("火影忍者Online", this);
    titleLabel_->setObjectName("SideTitle");
    head->addWidget(titleLabel_);
    head->addStretch();
    collapseBtn_ = new QPushButton("«", this);
    collapseBtn_->setObjectName("CollapseButton");
    collapseBtn_->setFixedSize(22, 22);
    collapseBtn_->setToolTip("收起 / 展开导航栏");
    head->addWidget(collapseBtn_);
    lay->addLayout(head);

    // 主体（收缩时隐藏）
    body_ = new QWidget(this);
    QVBoxLayout* bl = new QVBoxLayout(body_);
    bl->setContentsMargins(0, 2, 0, 0);
    bl->setSpacing(8);

    // 区块标题 + 添加账号按钮
    QHBoxLayout* sec = new QHBoxLayout;
    QLabel* secTitle = new QLabel("我的账号", body_);
    secTitle->setObjectName("SectionTitle");
    sec->addWidget(secTitle);
    sec->addStretch();
    QPushButton* addBtn = new QPushButton("＋ 添加账号", body_);
    addBtn->setObjectName("AddAccountButton");
    sec->addWidget(addBtn);
    bl->addLayout(sec);

    // 添加账号表单（默认隐藏）
    addForm_ = new QWidget(body_);
    addForm_->setObjectName("AddForm");
    addForm_->setVisible(false);
    QVBoxLayout* fl = new QVBoxLayout(addForm_);
    fl->setContentsMargins(10, 10, 10, 10);
    fl->setSpacing(6);

    // 扫码登录复选框
    addScanLogin_ = new QCheckBox("扫码登录（无需 QQ 号/密码）", addForm_);
    fl->addWidget(addScanLogin_);

    addRemark_ = new QLineEdit(addForm_);
    addRemark_->setPlaceholderText("备注（扫码登录必填）");
    fl->addWidget(addRemark_);
    addUsername_ = new QLineEdit(addForm_);
    addUsername_->setPlaceholderText("账号 / QQ号");
    fl->addWidget(addUsername_);
    addPassword_ = new QLineEdit(addForm_);
    addPassword_->setPlaceholderText("密码");
    addPassword_->setEchoMode(QLineEdit::Password);
    fl->addWidget(addPassword_);
    QHBoxLayout* fb = new QHBoxLayout;
    QPushButton* cancelBtn = new QPushButton("取消", addForm_);
    cancelBtn->setObjectName("SmallButton");
    fb->addWidget(cancelBtn);
    QPushButton* saveBtn = new QPushButton("保存", addForm_);
    saveBtn->setObjectName("SmallButtonPrimary");
    fb->addWidget(saveBtn);
    fl->addLayout(fb);
    bl->addWidget(addForm_);

    // 账号卡片滚动列表
    scroll_ = new QScrollArea(body_);
    scroll_->setWidgetResizable(true);
    QWidget* host = new QWidget(scroll_);
    accountsLayout_ = new QVBoxLayout(host);
    accountsLayout_->setContentsMargins(0, 0, 4, 0);
    accountsLayout_->setSpacing(8);

    // 空账号引导提示（无账号时显示）
    emptyLabel_ = new QLabel(
        "还没有账号\n\n点击上方「＋ 添加账号」\n保存后即可一键登录游戏", host);
    emptyLabel_->setObjectName("EmptyLabel");
    emptyLabel_->setAlignment(Qt::AlignCenter);
    emptyLabel_->setWordWrap(true);
    accountsLayout_->addWidget(emptyLabel_);

    accountsLayout_->addStretch();
    scroll_->setWidget(host);
    bl->addWidget(scroll_, 1);

    // 底部版本信息
    QLabel* ver = new QLabel("v0.1.0 · 阶段3", body_);
    ver->setObjectName("VersionLabel");
    ver->setAlignment(Qt::AlignCenter);
    bl->addWidget(ver);

    lay->addWidget(body_, 1);

    // 交互
    connect(collapseBtn_, &QPushButton::clicked, this, [this]() {
        SetCollapsed(!collapsed_);
    });
    connect(addBtn, &QPushButton::clicked, this, [this]() {
        addForm_->setVisible(!addForm_->isVisible());
        if (addForm_->isVisible())
            addRemark_->setFocus();
    });
    connect(cancelBtn, &QPushButton::clicked, this, [this]() {
        addForm_->setVisible(false);
    });

    // 勾选扫码登录：隐藏账号/密码，备注变为必填
    connect(addScanLogin_, &QCheckBox::toggled, this, [this](bool checked) {
        addUsername_->setVisible(!checked);
        addPassword_->setVisible(!checked);
        addRemark_->setPlaceholderText(checked
                                           ? "备注（扫码登录必填）"
                                           : "备注（可选）");
        if (checked)
            addRemark_->setFocus();
    });

    connect(saveBtn, &QPushButton::clicked, this, [this]() {
        AccountInfo info;
        info.remark = addRemark_->text();
        info.username = addUsername_->text();
        info.password = addPassword_->text();
        info.scanLogin = addScanLogin_->isChecked();
        if (info.scanLogin) {
            // 扫码登录：备注必填
            if (info.remark.trimmed().isEmpty())
                return;
            info.username.clear();
            info.password.clear();
        } else {
            // QQ 登录：账号必填
            if (info.username.isEmpty())
                return;
        }
        AddAccountToUi(info);
        SaveAll();
        addRemark_->clear();
        addUsername_->clear();
        addPassword_->clear();
        addScanLogin_->setChecked(false);
        addForm_->setVisible(false);
    });
}

void SideNav::LoadFromStore(const QVector<AccountInfo>& accounts) {
    for (const AccountInfo& a : accounts)
        AddAccountToUi(a);
}

void SideNav::AddAccountToUi(const AccountInfo& info) {
    // 隐藏空状态引导
    emptyLabel_->hide();

    AccountCard* card = new AccountCard(info, scroll_);
    accountsLayout_->insertWidget(accountsLayout_->count() - 1, card);

    connect(card, &AccountCard::StartRequested, this,
            &SideNav::StartRequested);
    connect(card, &AccountCard::Edited, this, [this]() {
        SaveAll();
    });
    connect(card, &AccountCard::DeleteRequested, this, [this, card]() {
        accountsLayout_->removeWidget(card);
        card->deleteLater();
        SaveAll();
        // 若没有账号了，重新显示引导
        if (Accounts().isEmpty())
            emptyLabel_->show();
    });
}

QVector<AccountInfo> SideNav::Accounts() const {
    QVector<AccountInfo> result;
    const int count = accountsLayout_->count();
    for (int i = 0; i < count; ++i) {
        QLayoutItem* item = accountsLayout_->itemAt(i);
        QWidget* w = item ? item->widget() : nullptr;
        AccountCard* card = qobject_cast<AccountCard*>(w);
        if (card)
            result.append(card->Info());
    }
    return result;
}

void SideNav::SaveAll() {
    AccountStore store;
    store.SaveAll(Accounts());
}

void SideNav::SetCollapsed(bool collapsed) {
    if (collapsed_ == collapsed)
        return;
    collapsed_ = collapsed;
    if (collapsed) {
        setFixedWidth(kCollapsedWidth);
        collapseBtn_->setText("»");
        titleLabel_->hide();
        body_->hide();
    } else {
        setFixedWidth(kExpandedWidth);
        collapseBtn_->setText("«");
        titleLabel_->show();
        body_->show();
    }
    emit CollapseChanged(collapsed_);
}
