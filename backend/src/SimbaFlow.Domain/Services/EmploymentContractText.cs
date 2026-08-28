namespace SimbaFlow.Domain.Services;

/// <summary>
/// Clause text of the MoLS Standard Employment Contract for Ethiopian domestic workers bound for
/// the Kingdom of Saudi Arabia.
///
/// This wording is legally fixed — it is transcribed from the approved bilingual contract, not
/// paraphrased, because the generated document is the one the worker signs and the embassies stamp.
/// Only the party details, dates, wage and reference numbers vary per candidate; everything here is
/// constant. Kept in the domain so it can be reviewed and diffed as text rather than buried in
/// layout code.
/// </summary>
public sealed record ContractClause(string Number, string TitleEn, string TitleAr, string[] BodyEn);

public static class EmploymentContractText
{
    public const string TitleEn = "STANDARD EMPLOYMENT CONTRACT FOR ETHIOPIA DOMESTIC WORKERS (DW) BOUND FOR THE KINGDOM OF SAUDI ARABIA";
    public const string TitleAr = "عقد العمل الخاص بالعمالة المنزلية من اثيوبيا المغادرة للمملكة العربية السعودية";

    public const string BindingEn =
        "The employer and the DSW hereby voluntarily bind themselves to the following terms and conditions:";
    public const string BindingAr =
        "يلتزم صاحب العمل والعامل المنزلي / العاملة المنزلية بموجب هذا العقد طوعا بالأحكام والشروط التالية:";

    public static readonly ContractClause[] Clauses =
    [
        new("1", "Preamble", "تمهيد",
        [
            "Whereas the Employer wishes to benefit from the services of the DW according to the DW's scientific and practical capabilities, and since the DW has the necessary qualifications to carry out the work specified herein, this Contract has been concluded in accordance with the following provisions."
        ]),

        new("2", "Site of Employment", "موقع العمل للعامل المنزلي",
        [
            "City of work: {CITY_OF_WORK}",
            "In case of any change in the site of employment, the Saudi Recruitment Office shall inform the same to the Embassy of Ethiopia and the Recruitment Agency in Ethiopia."
        ]),

        new("3", "Contract Term", "مدة العقد",
        [
            "The term of the Contract shall start from the date of the arrival of the DW in the Kingdom of Saudi Arabia.",
            "Contract duration: {CONTRACT_PERIOD}"
        ]),

        new("4", "Type of Work the DW is Committed to Perform", "نوع العمل الذي يلتزم العامل المنزلي بأدائه",
        [
            "Upon this Contract, the DW shall be hired as {POSITION}."
        ]),

        new("5", "Probationary Period", "مدة التجربة",
        [
            "A. The DW shall be on probation for (a period of a maximum of 90 days to be determined) during which the Employer will verify that the DW is professionally sufficient, and he/she has proper personal conduct.",
            "B. During the probationary period, the Employer may at his/her sole discretion terminate the Contract without any liability if it is proved that the DW is professionally insufficient.",
            "C. The DW may not be placed on probation by the Employer more than once, unless the two parties agree that the DW shall be employed in a different job than his/her first agreed job."
        ]),

        new("6", "Wage", "الأجر",
        [
            "A. A specified monthly wage shall be paid as of the work start date.",
            "B. The Employer and the DW agree on a fixed monthly wage of {WAGE} that shall be paid regularly at the end of each month by transferring it to a private bank account for the DW, unless the DW wishes the wage to be transferred to a specific bank account or to be transferred according to one of the channels specified by the Ministry.",
            "C. The Employer shall help the DW to open a bank account in the Kingdom of Saudi Arabia subject to the applicable rules of the Saudi Central Bank for depositing the wages regularly every month, unless the DW wished otherwise.",
            "D. The Employer shall provide pay slips, bank statements, or their equivalent to the DW."
        ]),

        new("7", "Rest Period", "مدة الراحة",
        [
            "A. The Employer shall allow the DW to enjoy daily rest for at least (9) hours a day, and in case of regular overtime work, the DW shall be paid for it at the rate applicable in the Kingdom of Saudi Arabia.",
            "B. The DW shall be entitled to obtain a (24) hours rest per week."
        ]),

        new("8", "Vacations and Leaves", "الإجازات",
        [
            "A. The DW is entitled to a paid leave of thirty (30) days if he/she completed two years of service and wishes to renew the employment contract for a similar period. The Employer shall bear the cost of a round trip economy class air ticket for the DW to travel to his/her country.",
            "B. The DW is entitled to a paid leave of no more than thirty (30) days per year, based on a medical report proving his/her need for the leave. In case the DW suffers from a serious illness or disability, or if he/she became incapable of completing the Contract, the Employer shall, at his expense, repatriate the DW to his/her original country and the DW shall be paid the wages due till the date of departure from the Kingdom of Saudi Arabia."
        ]),

        new("9", "Contract Expiration or Termination", "انتهاء وإنهاء العقد أو فسخه",
        [
            "A. This contract shall terminate automatically by the force of law in the following cases:",
            "   - Its term expired without being renewed.",
            "   - The death of the Employer; if the Employer's family wishes the DW to remain, they should refer to the Labor Office to correct the Employer's name.",
            "   - The death of the DW; in this case, the First Party shall bear the expenses of transporting the body of the Second Party and transferring it to the entity where the Contract was concluded or from which the DW was recruited (if necessary), unless he/she is buried inside the Kingdom of Saudi Arabia upon the consent of his/her relatives and in coordination with his/her embassy.",
            "B. The First Party has the right to terminate the Contract without notifying the Second Party in the following cases:",
            "   - A return visa is issued, and the period specified for the DW's return to the Kingdom of Saudi Arabia has expired.",
            "   - If a final exit is issued and the DW is not inside the Kingdom of Saudi Arabia.",
            "   - If a notice of absence is issued.",
            "C. The Employer shall bear all costs of returning the DW to his/her country in the following expiration and/or termination cases:",
            "   - Upon the expiry of the Contract term and the Second Party's desire to return to his/her country.",
            "   - If the Contract is terminated by the Employer for an illegitimate reason.",
            "   - If the Contract is terminated by the DW for a legitimate reason.",
            "   - Upon the decisions issued by relevant committees.",
            "D. The DW shall bear all costs of returning to his/her country in the following cases:",
            "   - If the DW is unable to carry out the work.",
            "   - If the DW wishes to return without a legitimate reason.",
            "   - If the Contract is terminated by the DW without a legitimate reason."
        ]),

        new("10", "Employer Obligations", "التزامات صاحب العمل",
        [
            "A. Not to assign the DW to carry out duties other than the ones agreed in the Contract, except in cases of necessity; provided that the work assigned to the DW is not fundamentally different from his/her original work.",
            "B. Not to assign the DW to carry out any dangerous work that threatens his/her health or physical safety or impairs his/her human dignity.",
            "C. Pay the DW the agreed wage at the end of each month, unless otherwise agreed by the parties.",
            "D. Provide adequate housing for the DW.",
            "E. Allow the DW to enjoy daily rest for at least (9) hours a day.",
            "F. Not to hire the DW to work for other parties or allow the DW to work for his own account.",
            "G. Comply with the regulations and instructions concerning the contractual relationship and the Regulation of Domestic Workers and enable the worker to refer to the competent authority in the event of any dispute regarding the provisions of this Contract.",
            "H. The First Party shall provide the DW a one-month paid leave if the DW completed two years of service and wishes to renew his/her employment Contract for a similar period.",
            "I. The Employer shall pay the cost of the DW's resident identity (Iqama), exit/entry visa, and final exit visa, including the renewals and penalties resulting from delays if any.",
            "J. The First Party shall not retain the passport of the Second Party, and in case the Second Party requests that the First Party retain his/her passport, the DW must sign a written declaration in Arabic and the language of the Second Party provided that the First Party returns the passport whenever requested by the Second Party.",
            "K. The DW shall be allowed to freely communicate with her/his family at her/his personal expense.",
            "L. It is not permissible to deduct from the DW wage except in the following cases, and a such deduction shall not exceed half of his/her wage:",
            "   - Costs of the damage he/she has intentionally or negligently caused.",
            "   - An advance payment the DW obtained from the Employer.",
            "   - Execution of a judicial ruling or administrative decision issued against the DW; unless it was stated in the judgment or administrative decision that the deduction exceeds half of his/her wage.",
            "M. In the event a deduction was made from the wages of the Second Party in the above-mentioned cases, this deduction must be documented in the pay slips of the Second Party.",
            "N. The First Party shall bear the consequences of violating the provisions of this Contract by his family members and those residing with him.",
            "O. In case the DW is absent from the site of employment, the Employer shall report the DW's absence to the nearest police station to the First Party's home for necessary actions. The Employer shall also report the DW's absence to the Saudi Recruitment Office in the Kingdom of Saudi Arabia; such office shall be responsible for reporting the absence to the embassy or consulate of the DW's country within (48) hours.",
            "P. The First Party shall enable the Second Party to have access to the committees for domestic worker dispute resolution, or the Ministry of Human Resources and Social Development, or the Saudi Recruitment Office if necessary.",
            "Q. In the event of a dispute arising with the DW during the period of his/her employment with the First Party, the Saudi Recruitment Office is obliged to transfer the DW to the approved authority for accommodating domestic works at that time.",
            "R. The Employer shall protect the rights of the DW in accordance with the laws and regulations in force in the Kingdom of Saudi Arabia, and the employer shall refrain from all forms of physical abuse and all forms of violence, whether physical, verbal, or sexual harassment."
        ]),

        new("11", "Domestic Worker Obligations", "التزامات العامل المنزلي",
        [
            "A. Perform the agreed tasks with due diligence.",
            "B. Follow the orders of the Employer and her/his family members as per the agreed working duties.",
            "C. Preserve the property of the Employer and her/his family members.",
            "D. Not to harm the Employer's family members, including children and the elderly.",
            "E. Keep the secrets of the Employer, family members, and other persons in the home to which the DW is exposed during work, and not disclose it to others.",
            "F. Not to refuse to do duties or leave the service premises without a legitimate reason.",
            "G. Not to work for his/her own account.",
            "H. Not to impair the dignity of the Employer and family members, and not to interfere with personal matters related to them.",
            "I. Respect Islamic religion and abide by the regulations in force in the Kingdom of Saudi Arabia, and the customs and traditions of Saudi society, and not to practice or engage in any activity that harms the family.",
            "J. The Second Party shall abide by the applicable regulations and instructions concerning the contractual relationship, comply with the Regulation of Domestic Workers, and refer to the committees for domestic worker dispute resolution in the event of any dispute.",
            "K. If the Second Party disputed with the First Party or a member of the Employer's family, and left the home of the Employer, the DW shall refer to the competent authorities."
        ]),

        new("12", "Settlement of Dispute and Governing Law", "تسوية المنازعات والقانون الواجب التطبيق",
        [
            "A. The parties to this contract shall endeavor to amicably resolve any dispute arising out of this Contract before escalating the dispute to the Ministry of Labor. However, they may resort immediately to Saudi competent authorities for litigation and/or resolution.",
            "B. The Employer is strictly prohibited from placing the DW outside the site of employment in the event of a dispute.",
            "C. This Contract is governed by the laws and regulations of the Government of the Kingdom of Saudi Arabia in terms of their interpretation, application, and resolution of disputes arising out of them."
        ]),

        new("13", "Saudi Recruitment Office Responsibility", "مسؤولية مكتب الاستقدام السعودي",
        [
            "First: The Saudi Recruitment Office is responsible for its recruited DW for no less than ninety (90) days, starting from the date of handing him/her over by the Employer, in the following cases:",
            "A. The DW's refusal to work for reasons not related to the Employer, provided that this is proven by a decision issued by the committees for domestic worker dispute resolution, or by the licensee.",
            "B. Absence from work.",
            "C. The DW's failure to carry out his/her duties, according to the employment contract concluded with him/her.",
            "D. The DW does not have the required experience.",
            "E. The DW suffers from an illness that prevents him/her from performing work duties.",
            "F. The reports of the medical and security examinations conducted for the DW are proven to be incorrect.",
            "G. Any other cases specified in the mediation contract approved by the Ministry.",
            "H. Second: In the event of a dispute arising with the DW during the period of his/her employment for the First Party, the Saudi Recruitment Office is obliged to transfer the DW to the approved authority for accommodating domestic works at that time."
        ]),

        new("14", "General Provisions", "أحكام عامة",
        [
            "A. The Saudi Recruitment Office shall be responsible, in coordination with the Recruitment Agency, for informing the Employer of the DW's arrival in the Kingdom of Saudi Arabia.",
            "B. The Employer or the Saudi Recruitment Office must inform the embassy or consulate of the country of the DW and the Recruitment Agency seventy-two (72) hours prior to the DW's flight for his departure or return.",
            "C. The DW shall work solely for the Employer and his immediate household.",
            "D. The DW is allowed to communicate freely with his/her family members or the embassy or consulate of his/her country without misusing the means of communication or allowing this to affect his/her work.",
            "E. The DW shall receive a copy of the Contract, and he/she shall be treated with respect and dignity.",
            "F. The Employer, or the DW on his/her own, shall report any displacement of a domestic worker outside the territory of the Kingdom of Saudi Arabia at least one week before going to the embassy or consulate of this domestic worker's country.",
            "G. In the event of war, civil unrest, or natural disasters during which the inability to complete the Contract is proven, the Employer shall expatriate the DW at her/his expense.",
            "H. After the Contract term expires and the DW wishes to return to his/her country, the Employer shall submit evidence to the Saudi Recruitment Office that the DW has received all his/her entitlements; the Employer and the DW shall sign a final discharge to this end, and the proof of such discharge can be invoked in both the Kingdom of Saudi Arabia and the DW's country.",
            "I. This Contract can be renewed by agreement between the DW and the Employer. If the Contract is renewed, the embassy or consulate of the country of the DW shall be provided with a copy of the resident identity (Iqama) renewed by the Employer or the Saudi Recruitment Office.",
            "J. This Employment Contract shall be the only valid contract. Any other contract entered into between the Employer and the worker in substitution of this Contract shall not be valid, except for the renewal of the Contract under the contracts approved by the Ministry of Human Resources and Social Development.",
            "K. The provisions of this Contract are valid, and the Contract is subject to the applicable regulations related to domestic labor in the Kingdom of Saudi Arabia.",
            "L. [Related to domestic workers]: I the candidate mentioned above in my personal capacity and based on my desire to work in the Kingdom of Saudi Arabia, I consent and accept with my full legal capacity without ignorance, to share my personal data with the competent authorities in the Kingdom of Saudi Arabia and all related parties, including but not limited to the Musaned Program and/or its operator company, and I fully consent and approve for my personal information to be used by the Musaned Program and/or its operators and to processed and shared with any third parties.",
            "M. The Contract is drawn up in Arabic and English, and in case of any conflict between the Arabic and English text, the Arabic text shall prevail."
        ])
    ];
}
