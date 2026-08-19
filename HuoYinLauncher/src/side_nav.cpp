#include "side_nav.h"

#include <QCheckBox>
#include <QHBoxLayout>
#include <QLabel>
#include <QLineEdit>
#include <QPushButton>
#include <QScrollArea>
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

    // 第一行：备注 + 删除
    QHBoxLayout* top = new QHBoxLayout;
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

    // 第二行：账号 + 密码
    QHBoxLayout* mid = new QHBoxLayout;
    username_ = new QLineEdit(info.username, this);
    username_->setPlaceholderText("账号 / QQ号");
    mid->addWidget(username_, 1);
    password_ = new QLineEdit(info.password, this);
    password_->setPlaceholderText("密码");
    password_->setEchoMode(QLineEdit::Password);
    mid->addWidget(password_, 1);
    lay->addLayout(mid);

    // 第三行：自动登录 + 编辑 + 开始游戏
    QHBoxLayout* ops = new QHBoxLayout;
    autoLogin_ = new QCheckBox("自动登录", this);
    ops->addWidget(autoLogin_);
    ops->addStretch();
    QPushButton* edit = new QPushButton("编辑", this);
    edit->setObjectName("CardButton");
    ops->addWidget(edit);
    QPushButton* start = new QPushButton("开始游戏", this);
    start->setObjectName("CardButtonPrimary");
    ops->addWidget(start);
    lay->addLayout(ops);

    ApplyMode();

    connect(del, &QPushButton::clicked, this, &AccountCard::DeleteRequested);
    connect(edit, &QPushButton::clicked, this, [this]() {
        editing_ = !editing_;
        ApplyMode();
        if (!editing_)
            emit Edited();
    });
    connect(start, &QPushButton::clicked, this, [this]() {
        emit StartRequested(Info());
    });
}

void AccountCard::ApplyMode() {
    const bool editable = editing_;
    remark_->setReadOnly(!editable);
    username_->setReadOnly(!editable);
    password_->setReadOnly(!editable);
    autoLogin_->setEnabled(editable);
    autoLogin_->setVisible(!editable || true);
    // 编辑/保存按钮文字由外部持有，这里仅控制字段状态
    if (!editable)
        password_->setEchoMode(QLineEdit::Password);
}

AccountInfo AccountCard::Info() const {
    AccountInfo i;
    i.remark = remark_->text();
    i.username = username_->text();
    i.password = password_->text();
    i.autoLogin = autoLogin_->isChecked();
    return i;
}

void AccountCard::SetInfo(const AccountInfo& info) {
    remark_->setText(info.remark);
    username_->setText(info.username);
    password_->setText(info.password);
    autoLogin_->setChecked(info.autoLogin);
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
    addRemark_ = new QLineEdit(addForm_);
    addRemark_->setPlaceholderText("备注（可选）");
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
    connect(saveBtn, &QPushButton::clicked, this, [this]() {
        AccountInfo info;
        info.remark = addRemark_->text();
        info.username = addUsername_->text();
        info.password = addPassword_->text();
        if (info.username.isEmpty())
            return;
        AddAccountToUi(info);
        SaveAll();
        addRemark_->clear();
        addUsername_->clear();
        addPassword_->clear();
        addForm_->setVisible(false);
    });
}

void SideNav::LoadFromStore(const QVector<AccountInfo>& accounts) {
    for (const AccountInfo& a : accounts)
        AddAccountToUi(a);
}

void SideNav::AddAccountToUi(const AccountInfo& info) {
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
