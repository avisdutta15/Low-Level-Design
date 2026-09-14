package _5_ManagementSystems._2_LibraryManagementSystem.entities.search;

public class SearchCriteria {
    private String title;
    private String author;
    private Integer publicationYear;
    private Boolean availableOnly;

    public SearchCriteria(){ }

    public SearchCriteria setTitle(String title) { this.title = title; return this; }
    public SearchCriteria setAuthor(String author) { this.author = author; return this; }
    public SearchCriteria setPublicationYear(Integer year) { this.publicationYear = year; return this; }
    public SearchCriteria setAvailableOnly(Boolean availableOnly) { this.availableOnly = availableOnly; return this; }

    public String getTitle() { return title; }
    public String getAuthor() { return author; }
    public Integer getPublicationYear() { return publicationYear; }
    public Boolean getAvailableOnly() { return availableOnly; }
}
